using System;
using System.Collections.Generic;
using System.Linq;

namespace GPA.Combat
{
    public enum Outcome { Ongoing, Victory, Defeat }

    // 대치형 턴제 전투. 속도 타임라인으로 다음 행동자를 정한다.
    // 사용: BeginTurn()으로 Current를 받고, UseArt()나 Meditate()로 행동한다.
    public class Battle
    {
        const double TurnScale = 1000;     // 행동 간격 = TurnScale / 속도
        const float Advantage = 1.3f, Disadvantage = 0.7f, CritMultiplier = 1.5f;
        const float QiDeviationPower = 1.5f, QiDeviationMisfire = 0.25f;
        const int MeditatePercent = 30;

        public readonly List<Combatant> Allies, Enemies;
        public readonly List<string> Log = new();
        public Combatant Current { get; private set; }
        readonly Random rng;

        public Battle(IEnumerable<Combatant> allies, IEnumerable<Combatant> enemies, Random rng = null)
        {
            Allies = allies.ToList();
            Enemies = enemies.ToList();
            this.rng = rng ?? new Random();
            foreach (var c in All) c.NextTurnAt = Delay(c);
        }

        IEnumerable<Combatant> All => Allies.Concat(Enemies);
        IEnumerable<Combatant> Alive => All.Where(c => !c.IsDown);
        List<Combatant> SideOf(Combatant c) => c.IsAlly ? Allies : Enemies;
        static double Delay(Combatant c) => TurnScale / Math.Max(1, c.Speed);

        public Outcome Outcome =>
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
                bool skip = c.StartTurn(Log);
                if (!skip && !c.IsDown) return Current = c;
                c.NextTurnAt += Delay(c);
            }
            return Current = null;
        }

        public bool CanUse(MartialArt art, Combatant target = null)
        {
            var a = Current;
            if (a == null || !a.Arts.Contains(art) || a.Cooldown(art) > 0) return false;
            return art.kind switch
            {
                ArtKind.Outer => target != null && !target.IsDown && target.IsAlly != a.IsAlly && Reachable(art, target),
                ArtKind.Movement => a.Inner >= art.innerCost,
                _ => false, // 심법·내공은 패시브
            };
        }

        // 근접 외공은 상대 전열이 남아 있으면 후열에 닿지 않는다.
        bool Reachable(MartialArt art, Combatant t) =>
            art.reachesBackRow || t.Row == Row.Front || SideOf(t).All(x => x.IsDown || x.Row == Row.Back);

        public bool UseArt(MartialArt art, Combatant target = null)
        {
            if (!CanUse(art, target)) return false;
            var a = Current;

            if (art.kind == ArtKind.Movement)
            {
                a.Inner -= art.innerCost;
                if (art.swapsRow) a.Row = a.Row == Row.Front ? Row.Back : Row.Front;
                a.SpeedBuff = art.speedBuff;
                a.EvasionBuff = art.evasionBuff;
                a.BuffTurns = art.buffTurns;
                a.Counter = art.counter;
                Log.Add($"{a.Name}: {art.displayName}");
            }
            else
            {
                if (a.Inner >= art.innerCost) a.Inner -= art.innerCost;
                else
                {
                    // 내력이 모자라도 무리해서 쓸 수 있다. 대가는 주화입마.
                    a.Inner = 0;
                    Log.Add($"{a.Name}: 내력을 무리하게 끌어올린다");
                    a.Inflict(StatusKind.QiDeviation, -1, 1f, rng, Log);
                }
                if (a.Has(StatusKind.QiDeviation) && rng.NextDouble() < QiDeviationMisfire)
                {
                    var friends = SideOf(a).Where(x => !x.IsDown && x != a).ToList();
                    if (friends.Count > 0) target = friends[rng.Next(friends.Count)];
                }
                Log.Add($"{a.Name}: {art.displayName} → {target.Name}");
                Strike(a, art, target);
                a.Stance = art.affinity;
            }

            if (art.cooldown > 0) a.StartCooldown(art);
            EndTurn();
            return true;
        }

        // 운기조식: 한 턴을 써서 내력을 크게 회복한다.
        public void Meditate()
        {
            var a = Current;
            if (a == null) return;
            int pct = MeditatePercent + a.Passives.Sum(p => p.meditateBonus);
            a.Inner = Math.Min(a.MaxInner, a.Inner + a.MaxInner * pct / 100);
            Log.Add($"{a.Name}: 운기조식");
            EndTurn();
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
                if (rng.NextDouble() < t.EvasionChance)
                {
                    Log.Add($"{t.Name}: 회피");
                    if (t.Counter) CounterAttack(t, a);
                    continue;
                }
                int dmg = Damage(a, art, t);
                t.TakeDamage(dmg);
                landed = true;
                Log.Add($"{t.Name}: 피해 {dmg}");
            }
            if (landed && !t.IsDown) t.Inflict(art.inflicts, art.inflictTurns, art.inflictChance, rng, Log);
        }

        // 반격은 첫 번째 외공으로 한 번, 내력 소모 없이.
        void CounterAttack(Combatant d, Combatant a)
        {
            var art = d.Arts.FirstOrDefault(x => x.kind == ArtKind.Outer);
            if (art == null) return;
            int dmg = Damage(d, art, a);
            a.TakeDamage(dmg);
            Log.Add($"{d.Name}: 반격, {a.Name} 피해 {dmg}");
        }

        int Damage(Combatant a, MartialArt art, Combatant t)
        {
            int stat = art.affinity switch
            {
                Affinity.Kwae => a.Stats.Agi,
                Affinity.Yu => a.Stats.Con,
                _ => a.Stats.Str,
            };
            float dmg = art.power * (1 + stat / 20f) * AffinityMultiplier(art.affinity, t.Stance);
            if (a.Has(StatusKind.QiDeviation)) dmg *= QiDeviationPower;
            if (rng.NextDouble() < a.CritChance) dmg *= CritMultiplier;
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
