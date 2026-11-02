using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

namespace GPA.Combat
{
    public enum Row { Front, Back }

    public enum StatKind { Outer, Inner, Light, Con, Wis, Virtue, Luck } // 외공, 내공, 경공, 근골, 오성, 인덕, 기연

    [Serializable]
    public class Stats
    {
        [FormerlySerializedAs("Str")] public int Outer;
        public int Inner;
        [FormerlySerializedAs("Agi")] public int Light;
        public int Con, Wis, Virtue, Luck;

        public ref int Ref(StatKind k)
        {
            switch (k)
            {
                case StatKind.Outer: return ref Outer;
                case StatKind.Inner: return ref Inner;
                case StatKind.Light: return ref Light;
                case StatKind.Con: return ref Con;
                case StatKind.Wis: return ref Wis;
                case StatKind.Virtue: return ref Virtue;
                default: return ref Luck;
            }
        }
    }

    public enum MindFit { Common, Match, Weak, Clash } // 공용, 일치, 약화(기본 심법으로 문파 무공), 역행(정사 충돌)

    public class Status
    {
        public StatusKind Kind;
        public int Turns; // 음수면 전투 내내
    }

    // 전투에 참가한 한 명. 수치 공식은 임시값 (docs/05_능력치.md).
    public class Combatant
    {
        public readonly string Name;
        public string Sprite = "NinjaDark"; // 캐릭터 시트 폴더 이름
        public string Figure;               // 입상·초상화 이름. 없으면 Name을 쓴다
        public IReadOnlyList<ItemData> Gear = Array.Empty<ItemData>(); // 스탯 보너스는 Stats에 이미 들어 있다
        public InnerNature Nature;          // 내공 속성
        public MartialArt Mind;             // 운용 심법. 주인공만 있고, 없으면 계열을 따지지 않는다
        public IReadOnlyDictionary<SkillKind, int> Skills; // 기예. 주인공과 심마만 있고, 없으면 기예를 따지지 않는다
        public readonly Dictionary<SkillKind, int> SkillUses = new(); // 전투 후 기예 정산용
        public readonly bool IsAlly;
        public readonly Stats Stats;
        public readonly IReadOnlyList<MartialArt> Arts;
        public Row Row;
        public int Lane; // 가로 칸 0~2. 전열과 합쳐 진영마다 3x2 격자다
        public int Hp, Inner;
        public Affinity Stance; // 마지막으로 쓴 외공의 속성. 맞을 때 상성 판정에 쓰인다
        public bool InnerInjured { get; private set; } // 내상: 전투가 끝나도 남는다
        public double NextTurnAt;
        public readonly List<Status> Statuses = new();
        public int SpeedBuff, EvasionBuff, BuffTurns;
        public int ArtGuard, MoveGuard; // 외공의 방어(다음 자기 턴까지), 경공의 방어(지속 턴 동안)
        public bool Counter;
        readonly Dictionary<MartialArt, int> cooldowns = new();

        public readonly int Realm; // 경지 단계 (0 = 삼류)
        readonly IReadOnlyDictionary<MartialArt, int> mastery;
        public readonly Dictionary<MartialArt, int> UseCounts = new(); // 전투 후 숙련도 정산용

        public Combatant(string name, bool isAlly, Stats stats, IEnumerable<MartialArt> arts, Row row = Row.Front, bool innerInjured = false,
            int realm = 0, IReadOnlyDictionary<MartialArt, int> mastery = null)
        {
            Name = name;
            IsAlly = isAlly;
            Stats = stats;
            Arts = arts.ToList();
            Row = row;
            InnerInjured = innerInjured;
            Realm = realm;
            this.mastery = mastery;
            Hp = MaxHp;
            Inner = MaxInner;
        }

        public IEnumerable<MartialArt> Passives => Arts.Where(a => a.kind is ArtKind.Mind or ArtKind.Inner);
        public int MaxHp => 50 + Stats.Con * 10 + Realm * 30;
        public int MaxInner => (int)((20 + Stats.Con * 5 + Realm * 20 + Passives.Sum(a => a.maxInnerBonus)) * (InnerInjured ? 0.7f : 1f));

        // 숙련도 효과: 3성 내력 소모 20% 감소, 6성 상태이상 확률 +20%p, 성마다 위력 +2%, 10성(대성) 위력 +20%
        public int MasteryOf(MartialArt art) => mastery != null && mastery.TryGetValue(art, out var m) ? m : 1;
        public int CostOf(MartialArt art) => MasteryOf(art) >= 3 ? art.innerCost * 8 / 10 : art.innerCost;
        public float PowerMultiplier(MartialArt art) => 1 + 0.02f * (MasteryOf(art) - 1) + (MasteryOf(art) >= 10 ? 0.2f : 0);
        public float InflictBonus(MartialArt art) => MasteryOf(art) >= 6 ? 0.2f : 0;
        internal void RecordUse(MartialArt art) => UseCounts[art] = UseCounts.TryGetValue(art, out var n) ? n + 1 : 1;

        // 장비: 공격 %는 합산하고, 무기가 무공과 맞으면 곱해서 더 오른다.
        // 방어 %는 장비와 무공·경공을 합산하되 60%까지.
        public const float WeaponMatch = 1.1f;
        const int MaxDefense = 60;
        public float GearMultiplier(MartialArt art) =>
            (1 + Gear.Sum(i => i.attack) / 100f) * (Gear.Any(i => i.weapon != WeaponType.None && i.weapon == art.weapon) ? WeaponMatch : 1);
        // 운용 심법과 무공의 궁합. 수치는 임시값 (docs/04_무공.md).
        public const float MatchPower = 1.1f, WeakPower = 0.8f, ClashPower = 0.7f;

        public MindFit FitOf(MartialArt art)
        {
            if (Mind == null) return MindFit.Common;
            if (Mind.lineage == Lineage.Universal) return MindFit.Match;
            if (art.lineage == Lineage.Common) return MindFit.Common;
            if (art.lineage == Mind.lineage) return MindFit.Match;
            return Mind.lineage == Lineage.Common ? MindFit.Weak : MindFit.Clash;
        }

        public float MindMultiplier(MartialArt art) => FitOf(art) switch
        {
            MindFit.Match => MatchPower,
            MindFit.Weak => WeakPower,
            MindFit.Clash => ClashPower,
            _ => 1,
        };

        // 기예: 1당 계통 위력 +0.5%, 상대 회피 -0.2%p. 해독은 1당 중독 확률 -1% (최대 60%).
        public int SkillOf(SkillKind k) => Skills != null && Skills.TryGetValue(k, out var v) ? v : 0;
        public bool MeetsSkill(MartialArt art) => Skills == null || SkillOf(art.skill) >= art.skillNeed;
        public float SkillMultiplier(MartialArt art) => 1 + SkillOf(art.skill) * 0.005f;
        public float SkillAccuracy(MartialArt art) => SkillOf(art.skill) * 0.002f;
        public float PoisonGuard => 1 - Math.Min(0.6f, SkillOf(SkillKind.Antidote) * 0.01f);
        public float PoisonBonus(StatusKind k) => k == StatusKind.Poison ? SkillOf(SkillKind.Poison) * 0.005f : 0;

        internal void Practice(SkillKind k)
        {
            if (k != SkillKind.None && Skills != null) SkillUses[k] = SkillUses.TryGetValue(k, out var n) ? n + 1 : 1;
        }

        public static string SkillLabel(SkillKind k) => k switch
        {
            SkillKind.Sword => "검술",
            SkillKind.Blade => "도법",
            SkillKind.Palm => "장법",
            SkillKind.Finger => "지법",
            SkillKind.Fist => "권법",
            SkillKind.Kick => "각법",
            SkillKind.Hidden => "암기",
            SkillKind.Medicine => "의료",
            SkillKind.Poison => "독술",
            SkillKind.Antidote => "해독",
            _ => "",
        };

        // 내공 속성: 음과 양이 맞붙으면 서로 더 아프다. 조화는 음이나 양에게 더 주고, 상극을 받지 않는다.
        // 양은 강(剛), 음은 유(柔) 무공에 힘이 실린다. 조화는 둘 다. 공용 기본기에는 실리지 않는다.
        public const float NatureClash = 1.15f, NatureArt = 1.1f;

        public float NatureMultiplier(Combatant t) => (Nature, t.Nature) switch
        {
            (InnerNature.Yin, InnerNature.Yang) or (InnerNature.Yang, InnerNature.Yin) => NatureClash,
            (InnerNature.Harmony, InnerNature.Yin or InnerNature.Yang) => NatureClash,
            _ => 1,
        };

        public float NatureArtBonus(MartialArt art) => art.lineage == Lineage.Common ? 1 : (Nature, art.affinity) switch
        {
            (InnerNature.Yang or InnerNature.Harmony, Affinity.Gang) => NatureArt,
            (InnerNature.Yin or InnerNature.Harmony, Affinity.Yu) => NatureArt,
            _ => 1,
        };

        public static string NatureLabel(InnerNature n) => n switch
        {
            InnerNature.Yin => "음",
            InnerNature.Yang => "양",
            InnerNature.Harmony => "음양조화",
            _ => "",
        };

        public int Guard => ArtGuard + MoveGuard;
        public float DamageTaken => 1 - Math.Min(MaxDefense, Gear.Sum(i => i.defense) + Guard) / 100f;

        public int Speed => Stats.Light + SpeedBuff;
        public float EvasionChance => Math.Min(0.5f, Stats.Light * 0.005f + EvasionBuff / 100f);
        public float CritChance => Stats.Luck * 0.01f;
        public bool IsDown => Hp <= 0;

        public bool Has(StatusKind k) => Statuses.Any(s => s.Kind == k);
        public bool Resists(StatusKind k) => Passives.Any(a => a.resists.Contains(k)) || Gear.Any(i => i.resists.Contains(k));
        public int Cooldown(MartialArt art) => cooldowns.TryGetValue(art, out var t) ? t : 0;
        // 자기 턴 시작마다 1씩 줄어서 0이 되면 쓸 수 있다. +1은 사용한 턴 다음부터 세기 위해서.
        internal void StartCooldown(MartialArt art) => cooldowns[art] = art.cooldown + 1;

        public void TakeDamage(int dmg) => Hp = Math.Max(0, Hp - dmg);

        // 자기 턴 시작 처리. 행동할 수 없으면 true.
        internal bool StartTurn(List<BattleEvent> events)
        {
            bool pressured = Has(StatusKind.Pressure);
            ArtGuard = 0;
            foreach (var s in Statuses.ToList())
            {
                int pct = s.Kind switch { StatusKind.Bleed => 8, StatusKind.Poison => 8, StatusKind.QiDeviation => 5, _ => 0 };
                if (pct > 0)
                {
                    int dmg = Math.Max(1, MaxHp * pct / 100);
                    TakeDamage(dmg);
                    if (s.Kind == StatusKind.Poison) Practice(SkillKind.Antidote); // 앓으면서 배운다
                    events.Add(new BattleEvent { Kind = EventKind.Dot, Target = this, Amount = dmg, Status = s.Kind, Text = $"{Name}: {Label(s.Kind)}{Ro(Label(s.Kind))} 체력 {dmg} 감소" });
                }
                if (s.Turns > 0 && --s.Turns == 0) Statuses.Remove(s);
            }

            Inner = Math.Min(MaxInner, Inner + MaxInner * 5 / 100);
            foreach (var art in cooldowns.Keys.ToList())
                if (cooldowns[art] > 0) cooldowns[art]--;
            if (BuffTurns > 0 && --BuffTurns == 0)
            {
                SpeedBuff = EvasionBuff = MoveGuard = 0;
                Counter = false;
            }

            if (pressured) events.Add(new BattleEvent { Source = this, Text = $"{Name}: 점혈되어 움직이지 못한다" });
            return pressured;
        }

        internal void Inflict(StatusKind k, int turns, float chance, Random rng, List<BattleEvent> events)
        {
            if (k == StatusKind.None) return;
            if (Resists(k)) chance /= 2;
            if (k == StatusKind.Poison) chance *= PoisonGuard;
            if (rng.NextDouble() >= chance) return;

            if (k == StatusKind.InnerInjury)
            {
                InnerInjured = true;
                Inner = Math.Min(Inner, MaxInner);
            }
            else
            {
                Statuses.RemoveAll(s => s.Kind == k);
                Statuses.Add(new Status { Kind = k, Turns = turns });
            }
            events.Add(new BattleEvent { Kind = EventKind.Status, Target = this, Status = k, Text = $"{Name}: {Label(k)}" });
        }

        bool Suffers(StatusKind k) => k == StatusKind.InnerInjury ? InnerInjured : Has(k);

        // 먹어도 아무 효과 없는 약은 쓸 수 없다.
        public bool Needs(ItemData item) =>
            (item.heal > 0 && Hp < MaxHp) || (item.restoreInner > 0 && Inner < MaxInner) || item.cures.Any(Suffers);

        // medicine: 약을 쓰는 사람의 의료. 1당 회복량 +1%.
        internal void Apply(ItemData item, List<BattleEvent> events, int medicine = 0)
        {
            int hp = Math.Min(MaxHp - Hp, MaxHp * item.heal * (100 + medicine) / 10000);
            if (hp > 0)
            {
                Hp += hp;
                events.Add(new BattleEvent { Kind = EventKind.Heal, Target = this, Amount = hp, Text = $"{Name}: 체력 {hp} 회복" });
            }
            foreach (var k in item.cures.Where(Suffers).ToList())
            {
                if (k == StatusKind.InnerInjury) InnerInjured = false;
                Statuses.RemoveAll(s => s.Kind == k);
                events.Add(new BattleEvent { Target = this, Text = $"{Name}: {Label(k)} 치료" });
            }
            int inner = Math.Min(MaxInner - Inner, MaxInner * item.restoreInner / 100);
            if (inner > 0)
            {
                Inner += inner;
                events.Add(new BattleEvent { Target = this, Text = $"{Name}: 내력 {inner} 회복" });
            }
        }

        // 전투 종료: 주화입마는 내상으로 바뀌고 나머지 상태는 사라진다.
        internal void EndBattle()
        {
            if (Has(StatusKind.QiDeviation)) InnerInjured = true;
            Statuses.Clear();
            cooldowns.Clear();
            SpeedBuff = EvasionBuff = BuffTurns = ArtGuard = MoveGuard = 0;
            Counter = false;
            Inner = Math.Min(Inner, MaxInner);
        }

        // 조사 '으로/로': 받침이 없거나 ㄹ이면 '로'
        static string Ro(string word)
        {
            int jong = (word[^1] - 0xAC00) % 28;
            return jong == 0 || jong == 8 ? "로" : "으로";
        }

        public static string Label(StatusKind k) => k switch
        {
            StatusKind.Bleed => "출혈",
            StatusKind.Poison => "중독",
            StatusKind.Pressure => "점혈",
            StatusKind.InnerInjury => "내상",
            StatusKind.QiDeviation => "주화입마",
            _ => "",
        };
    }
}
