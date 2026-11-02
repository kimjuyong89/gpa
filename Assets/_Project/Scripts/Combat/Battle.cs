using System;
using System.Collections.Generic;
using System.Linq;

namespace GPA.Combat
{
    public enum Outcome { Ongoing, Victory, Defeat, Fled }
    public enum EventKind { Text, Act, Hit, Dodge, Status, Dot, Heal }

    // 전투에서 일어난 일 하나. 화면은 이걸 차례로 재생한다.
    public class BattleEvent
    {
        public EventKind Kind;
        public Combatant Source, Target;
        public int Amount;
        public bool Crit;
        public StatusKind Status;
        public string Text;
    }

    // 대치형 턴제 전투. 속도 타임라인으로 다음 행동자를 정한다.
    // 사용: BeginTurn()으로 Current를 받고, UseArt()나 Meditate()로 행동한다.
    public class Battle
    {
        const double TurnScale = 1000;     // 행동 간격 = TurnScale / 속도
        const float Advantage = 1.3f, Disadvantage = 0.7f, CritMultiplier = 1.5f;
        const float QiDeviationPower = 1.5f, QiDeviationMisfire = 0.25f, ClashDeviation = 0.15f;
        const int MeditatePercent = 30;
        public const int LaneCount = 3;
        public const int ActionCost = 10; // 응급치료·독 뿌리기의 내력

        public readonly List<Combatant> Allies, Enemies;
        public readonly List<BattleEvent> Events = new();
        public Combatant Current { get; private set; }
        public float DamageSpread = 0.15f; // 피해 편차 ±15%. 테스트에서 0으로 두면 결정적이다
        public Inventory Bag = new();      // 주인공의 소지품. 전투에서 쓴 약은 바로 사라진다
        public bool Tempting;              // 심마 전투: 한 번, 유혹에 넘어갈 수 있다
        public bool AlwaysFlee;            // 숨은 사건: 표시된 확률과 상관없이 달아난다
        readonly Random rng;
        bool fled;

        public Battle(IEnumerable<Combatant> allies, IEnumerable<Combatant> enemies, Random rng = null)
        {
            Allies = allies.ToList();
            Enemies = enemies.ToList();
            this.rng = rng ?? new Random();
            Arrange(Allies);
            Arrange(Enemies);
            foreach (var c in All) c.NextTurnAt = Delay(c);
        }

        // 한 칸에 한 명. 겹치면 같은 열의 옆 칸, 그다음 다른 열로 옮긴다.
        static void Arrange(List<Combatant> side)
        {
            var taken = new HashSet<(Row, int)>();
            foreach (var c in side)
            {
                var other = c.Row == Row.Front ? Row.Back : Row.Front;
                var cell = new[] { c.Row, other }
                    .SelectMany(r => Enumerable.Range(0, LaneCount).Select(i => (r, (c.Lane + i) % LaneCount)))
                    .First(taken.Add);
                (c.Row, c.Lane) = cell;
            }
        }

        IEnumerable<Combatant> All => Allies.Concat(Enemies);
        IEnumerable<Combatant> Alive => All.Where(c => !c.IsDown);
        List<Combatant> SideOf(Combatant c) => c.IsAlly ? Allies : Enemies;
        static double Delay(Combatant c) => TurnScale / Math.Max(1, c.Speed);

        public Outcome Outcome =>
            fled ? Outcome.Fled :
            Enemies.All(e => e.IsDown) ? Outcome.Victory :
            Allies.All(a => a.IsDown) ? Outcome.Defeat : Outcome.Ongoing;

        // 시각이 가장 빠른 사람. 같으면 아군 먼저, 그다음 목록 순서.
        static Combatant Earliest(IEnumerable<(Combatant c, double at)> xs) =>
            xs.OrderBy(x => x.at).ThenBy(x => x.c.IsAlly ? 0 : 1).First().c;

        // UI 타임라인용: 앞으로 행동할 순서 (현재 속도 기준)
        public List<Combatant> PreviewOrder(int count)
        {
            var next = Alive.Select(c => (c, at: c.NextTurnAt)).ToList();
            var order = new List<Combatant>();
            while (order.Count < count && next.Count > 0)
            {
                var c = Earliest(next);
                order.Add(c);
                int i = next.FindIndex(x => x.c == c);
                next[i] = (c, next[i].at + Delay(c));
            }
            return order;
        }

        // 다음 행동자를 정한다. 점혈되었거나 지속 피해로 쓰러지면 건너뛴다. 전투가 끝났으면 null.
        public Combatant BeginTurn()
        {
            while (Outcome == Outcome.Ongoing)
            {
                var c = Earliest(Alive.Select(x => (x, x.NextTurnAt)));
                bool skip = c.StartTurn(Events);
                if (!skip && !c.IsDown) return Current = c;
                c.NextTurnAt += Delay(c);
            }
            return Current = null;
        }

        public bool CanUse(MartialArt art, Combatant target = null)
        {
            var a = Current;
            if (a == null || !a.Arts.Contains(art) || a.Cooldown(art) > 0 || !a.MeetsSkill(art)) return false;
            return art.kind switch
            {
                ArtKind.Outer => target != null && !target.IsDown && target.IsAlly != a.IsAlly && Reachable(art, target),
                ArtKind.Movement => a.Inner >= a.CostOf(art),
                _ => false, // 심법·내공은 패시브
            };
        }

        // 근접 외공은 같은 가로줄의 전열이 살아 있으면 후열에 닿지 않는다.
        bool Reachable(MartialArt art, Combatant t) =>
            art.reachesBackRow || t.Row == Row.Front || !SideOf(t).Any(x => !x.IsDown && x.Row == Row.Front && x.Lane == t.Lane);

        public bool UseArt(MartialArt art, Combatant target = null)
        {
            if (!CanUse(art, target)) return false;
            var a = Current;

            if (art.kind == ArtKind.Movement)
            {
                a.Inner -= a.CostOf(art);
                if (art.swapsRow) SwapRow(a);
                a.SpeedBuff = art.speedBuff;
                a.EvasionBuff = art.evasionBuff;
                a.MoveGuard = art.guard;
                a.BuffTurns = art.buffTurns;
                a.Counter = art.counter;
                Events.Add(new BattleEvent { Kind = EventKind.Act, Source = a, Text = $"{a.Name}: {art.displayName}" });
            }
            else
            {
                if (a.Inner >= a.CostOf(art)) a.Inner -= a.CostOf(art);
                else
                {
                    // 내력이 모자라도 무리해서 쓸 수 있다. 대가는 주화입마.
                    a.Inner = 0;
                    Events.Add(new BattleEvent { Source = a, Text = $"{a.Name}: 내력을 무리하게 끌어올린다" });
                    a.Inflict(StatusKind.QiDeviation, -1, 1f, rng, Events);
                }
                if (a.FitOf(art) == MindFit.Clash) a.Inflict(StatusKind.QiDeviation, -1, ClashDeviation, rng, Events); // 정사가 부딪힌다
                if (a.Has(StatusKind.QiDeviation) && rng.NextDouble() < QiDeviationMisfire)
                {
                    var friends = SideOf(a).Where(x => !x.IsDown && x != a).ToList();
                    target = friends.Count > 0 ? friends[rng.Next(friends.Count)] : null; // 혼자면 헛손질
                }
                Events.Add(new BattleEvent { Kind = EventKind.Act, Source = a, Target = target, Text = target != null ? $"{a.Name}: {art.displayName}" : $"{a.Name}: 기운이 엇나간다" });
                if (target != null) Strike(a, art, target);
                a.Stance = art.affinity;
                a.ArtGuard = art.guard;
            }

            a.RecordUse(art);
            a.Practice(art.skill);
            if (art.cooldown > 0) a.StartCooldown(art);
            EndTurn();
            return true;
        }

        // 같은 가로줄의 다른 열로 간다. 거기 있던 사람과 자리를 바꾼다.
        void SwapRow(Combatant a)
        {
            var to = a.Row == Row.Front ? Row.Back : Row.Front;
            var there = SideOf(a).FirstOrDefault(x => x.Row == to && x.Lane == a.Lane);
            if (there != null) there.Row = a.Row;
            a.Row = to;
        }

        // 운기조식: 한 턴을 써서 내력을 크게 회복한다.
        public void Meditate()
        {
            var a = Current;
            if (a == null) return;
            int pct = MeditatePercent + a.Passives.Sum(p => p.meditateBonus);
            a.Inner = Math.Min(a.MaxInner, a.Inner + a.MaxInner * pct / 100);
            Events.Add(new BattleEvent { Source = a, Text = $"{a.Name}: 운기조식" });
            EndTurn();
        }

        public IEnumerable<ItemData> UsableItems =>
            Bag.Items.Where(i => !i.IsGear && Allies.Any(t => CanUseItem(i, t)));

        // 약은 아군의 턴에 살아 있는 아군에게 쓴다.
        public bool CanUseItem(ItemData item, Combatant target) =>
            Current is { IsAlly: true } && Bag.Count(item) > 0 && target is { IsAlly: true, IsDown: false } && target.Needs(item);

        public bool UseItem(ItemData item, Combatant target)
        {
            if (!CanUseItem(item, target)) return false;
            var a = Current;
            Bag.Remove(item);
            Events.Add(new BattleEvent { Source = a, Text = $"{a.Name}: {item.name}" });
            target.Apply(item, Events, a.SkillOf(SkillKind.Medicine));
            a.Practice(SkillKind.Medicine);
            if (item.cures.Contains(StatusKind.Poison)) a.Practice(SkillKind.Antidote);
            EndTurn();
            return true;
        }

        // 응급치료: 내력으로 아군을 돌본다. 기본 5% + 의료 2당 1%.
        public bool CanFirstAid(Combatant t) =>
            Current is { IsAlly: true, Skills: not null } && Current.Inner >= ActionCost && t is { IsAlly: true, IsDown: false } && t.Hp < t.MaxHp;

        public void FirstAid(Combatant t)
        {
            if (!CanFirstAid(t)) return;
            var a = Current;
            a.Inner -= ActionCost;
            int hp = Math.Min(t.MaxHp - t.Hp, t.MaxHp * (5 + a.SkillOf(SkillKind.Medicine) / 2) / 100);
            t.Hp += hp;
            Events.Add(new BattleEvent { Source = a, Text = $"{a.Name}: 응급치료" });
            Events.Add(new BattleEvent { Kind = EventKind.Heal, Target = t, Amount = hp, Text = $"{t.Name}: 체력 {hp} 회복" });
            a.Practice(SkillKind.Medicine);
            EndTurn();
        }

        // 독 뿌리기: 어디서든 닿는다. 기본 30% + 독술 1당 1%p, 3턴.
        public bool CanThrowPoison(Combatant t) =>
            Current is { IsAlly: true, Skills: not null } && Current.Inner >= ActionCost && t is { IsAlly: false, IsDown: false };

        public void ThrowPoison(Combatant t)
        {
            if (!CanThrowPoison(t)) return;
            var a = Current;
            a.Inner -= ActionCost;
            Events.Add(new BattleEvent { Kind = EventKind.Act, Source = a, Target = t, Text = $"{a.Name}: 독 뿌리기" });
            t.Inflict(StatusKind.Poison, 3, 0.3f + a.SkillOf(SkillKind.Poison) * 0.01f, rng, Events);
            a.Practice(SkillKind.Poison);
            EndTurn();
        }

        public bool CanSuccumb => Tempting && Current is { IsAlly: true };

        // 유혹에 넘어간다: 체력을 40% 돌려받는 대신 주화입마(위력 1.5배, 턴마다 피해, 끝나면 내상)에 든다. 턴을 쓴다.
        public void Succumb()
        {
            if (!CanSuccumb) return;
            var a = Current;
            Tempting = false;
            Events.Add(new BattleEvent { Source = a, Text = "심마: 그래, 받아들여라" });
            int hp = Math.Min(a.MaxHp - a.Hp, a.MaxHp * 4 / 10);
            a.Hp += hp;
            if (hp > 0) Events.Add(new BattleEvent { Kind = EventKind.Heal, Target = a, Amount = hp, Text = $"{a.Name}: 체력 {hp} 회복" });
            a.Inflict(StatusKind.QiDeviation, -1, 2f, rng, Events); // 스스로 받아들이면 저항해도 걸린다
            EndTurn();
        }

        // 도주: 아군 평균 속도가 적보다 빠를수록 잘 된다 (20~90%). 실패하면 턴만 날린다.
        public float FleeChance
        {
            get
            {
                float mine = (float)Allies.Where(c => !c.IsDown).Average(c => c.Speed);
                float theirs = (float)Enemies.Where(c => !c.IsDown).Average(c => c.Speed);
                return Math.Min(0.9f, Math.Max(0.2f, 0.5f + (mine - theirs) * 0.05f));
            }
        }

        public void Flee()
        {
            var a = Current;
            if (a == null) return;
            fled = AlwaysFlee || rng.NextDouble() < FleeChance;
            Events.Add(new BattleEvent { Source = a, Text = fled ? $"{a.Name}: 몸을 빼 달아났다" : $"{a.Name}: 달아나지 못했다" });
            EndTurn();
        }

        // 적 AI: 내력이 되는 외공 중 하나를 무작위로 골라, 닿는 상대 중 체력이 가장 낮은 쪽을 친다(집중 공격).
        // 쓸 수 있는 외공이 없으면 운기조식.
        // ponytail: 상성·상태이상을 따지는 AI는 적 종류가 늘면 추가
        public void AutoAct()
        {
            var a = Current;
            if (a == null) return;
            var opponents = a.IsAlly ? Enemies : Allies;
            var arts = a.Arts
                .Where(art => art.kind == ArtKind.Outer && a.Inner >= a.CostOf(art) && opponents.Any(t => CanUse(art, t)))
                .ToList();
            if (arts.Count == 0)
            {
                Meditate();
                return;
            }
            var pick = arts[rng.Next(arts.Count)];
            var target = opponents.Where(t => CanUse(pick, t)).OrderBy(t => t.Hp).First();
            UseArt(pick, target);
        }

        // 전투가 끝난 뒤 한 번 부른다. 주화입마를 내상으로 바꾸는 등 뒷정리를 한다.
        public void Finish()
        {
            foreach (var c in All) c.EndBattle();
        }

        void EndTurn()
        {
            Current.NextTurnAt += Delay(Current);
            Current = null;
        }

        void Strike(Combatant a, MartialArt art, Combatant t)
        {
            bool landed = false;
            for (int i = 0; i < art.hits && !t.IsDown && !a.IsDown; i++)
            {
                if (rng.NextDouble() < t.EvasionChance - a.SkillAccuracy(art))
                {
                    Events.Add(new BattleEvent { Kind = EventKind.Dodge, Source = a, Target = t, Text = $"{t.Name}: 회피" });
                    if (t.Counter) CounterAttack(t, a);
                    continue;
                }
                int dmg = Damage(a, art, t, out bool crit);
                t.TakeDamage(dmg);
                landed = true;
                Events.Add(new BattleEvent { Kind = EventKind.Hit, Source = a, Target = t, Amount = dmg, Crit = crit, Text = $"{t.Name}: 피해 {dmg}" });
            }
            if (!landed || t.IsDown) return;
            t.Inflict(art.inflicts, art.inflictTurns, art.inflictChance + a.InflictBonus(art) + a.PoisonBonus(art.inflicts), rng, Events);
            foreach (var p in a.Passives) t.Inflict(p.onHit, p.onHitTurns, p.onHitChance + a.PoisonBonus(p.onHit), rng, Events);
            if (art.inflicts == StatusKind.Poison || a.Passives.Any(p => p.onHit == StatusKind.Poison)) a.Practice(SkillKind.Poison);
        }

        // 반격은 첫 번째 외공으로 한 번, 내력 소모 없이.
        void CounterAttack(Combatant d, Combatant a)
        {
            var art = d.Arts.FirstOrDefault(x => x.kind == ArtKind.Outer);
            if (art == null) return;
            int dmg = Damage(d, art, a, out bool crit);
            a.TakeDamage(dmg);
            Events.Add(new BattleEvent { Kind = EventKind.Hit, Source = d, Target = a, Amount = dmg, Crit = crit, Text = $"{d.Name}: 반격, {a.Name} 피해 {dmg}" });
        }

        int Damage(Combatant a, MartialArt art, Combatant t, out bool crit)
        {
            int stat = art.affinity switch
            {
                Affinity.Kwae => a.Stats.Light,
                Affinity.Yu => a.Stats.Con,
                _ => a.Stats.Outer,
            };
            float dmg = art.power * (1 + stat / 20f) * (1 + a.Stats.Inner / 40f) * a.PowerMultiplier(art) * a.GearMultiplier(art) * a.MindMultiplier(art) * a.SkillMultiplier(art) * a.NatureArtBonus(art) * a.NatureMultiplier(t) * t.DamageTaken * AffinityMultiplier(art.affinity, t.Stance);
            if (a.Has(StatusKind.QiDeviation)) dmg *= QiDeviationPower;
            crit = rng.NextDouble() < a.CritChance;
            if (crit) dmg *= CritMultiplier;
            dmg *= 1 + DamageSpread * (float)(rng.NextDouble() * 2 - 1);
            return Math.Max(1, (int)Math.Round(dmg));
        }

        // 강 → 쾌 → 유 → 강 순으로 이긴다.
        public static float AffinityMultiplier(Affinity attack, Affinity defense)
        {
            if (attack == Affinity.None || defense == Affinity.None || attack == defense) return 1f;
            bool wins = (attack, defense) is (Affinity.Gang, Affinity.Kwae) or (Affinity.Kwae, Affinity.Yu) or (Affinity.Yu, Affinity.Gang);
            return wins ? Advantage : Disadvantage;
        }
    }
}
