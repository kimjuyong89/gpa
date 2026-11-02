using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;

namespace GPA.Growth
{
    public enum Realm { Samryu, Iryu, Ilryu, Jeoljeong, ChoJeoljeong, Hwagyeong } // 삼류, 이류, 일류, 절정, 초절정, 화경
    public enum Training { Tonap, Sparring, Reading, Labor } // 토납, 대련, 독서, 단련·노동

    // 전투 밖에서 이어지는 캐릭터 상태. 수치는 임시값 (docs/05_능력치.md).
    public class Character
    {
        public const int InnCost = 20;
        public const int MaxSkill = 100;
        public const int SlotCount = 6; // 무기, 옷, 신발, 장갑, 장신구 2개. 번호는 ItemKind 순서

        public string Name;
        public int Appearance; // 외형 번호
        public Stats Stats;
        public Realm Realm;
        public int InnerPower; // 내공 축적량. 다 차면 경지 돌파를 시도할 수 있다
        public int Fatigue;
        public int Money;
        public bool InnerInjured;
        public bool Enlightened; // 큰 벽을 넘기 위한 깨달음. 이벤트가 켠다
        public readonly List<MartialArt> Arts = new();
        public readonly Inventory Bag = new();
        internal readonly Dictionary<SkillKind, int> SkillLevels = new(), SkillProgress = new(); // 세이브용으로 internal
        MartialArt activeMind;
        public readonly ItemData[] Equipped = new ItemData[SlotCount];
        internal readonly Dictionary<MartialArt, int> masteryExp = new(); // 세이브용으로 internal
        internal readonly int[] statProgress = new int[5];

        public Character(string name, Stats stats, IEnumerable<MartialArt> arts = null)
        {
            Name = name;
            Stats = stats;
            if (arts != null) Arts.AddRange(arts);
        }

        public static bool IsMental(MartialArt art) => art.kind is ArtKind.Mind or ArtKind.Inner;

        // 운용 심법: 전투에서 효과가 나고 일주천으로 느는 심법은 이것 하나다. 고른 적 없으면 먼저 배운 것.
        public MartialArt ActiveMind
        {
            get => activeMind != null && Arts.Contains(activeMind) ? activeMind : Arts.FirstOrDefault(IsMental);
            set => activeMind = value;
        }

        IEnumerable<MartialArt> BattleArts => Arts.Where(a => !IsMental(a) || a == ActiveMind);

        public int MaxFatigue => 100 + Stats.Con * 5;
        public MartialArt MovementArt => Arts.FirstOrDefault(a => a.kind == ArtKind.Movement); // 맵에서 쓰는 경공
        public int StatCap => 10 + (int)Realm * 10;
        public int InnerPowerToBreak => 100 * ((int)Realm + 1);
        public static bool IsWall(Realm current) => current is Realm.Ilryu or Realm.ChoJeoljeong; // 일류→절정, 초절정→화경

        // 무공별 숙련도 상한. 없으면 10성. 문파 무공은 일반 제자일 때 7성으로 묶인다.
        public readonly Dictionary<MartialArt, int> MasteryCaps = new();
        internal readonly Dictionary<MartialArt, int> Forged = new(); // 초식 연마로 넘은 가장 높은 관문

        public static bool IsFormGate(int level) => level is 6 or 9 or 10;
        static int ExpFor(MartialArt art, int level) => 25 * level * (level - 1) * (level > 5 ? Math.Max(1, art.masteryScale) : 1);
        int ExpOf(MartialArt art) => masteryExp.TryGetValue(art, out var e) ? e : 0;
        int CapOf(MartialArt art) => MasteryCaps.TryGetValue(art, out var c) ? c : 10;

        // 숙련도 n성에 필요한 누적 경험치: 25·n·(n-1)  (2성 50, 3성 150, … 10성 2250)
        // 상한에 걸려도 경험치는 계속 쌓여서, 상한이 풀리면 바로 반영된다.
        // 6·9·10성은 경험치가 차도 초식 연마를 넘어야 오른다.
        public int Mastery(MartialArt art)
        {
            int exp = ExpOf(art), cap = CapOf(art);
            int passed = Forged.TryGetValue(art, out var f) ? f : 0;
            int level = 1;
            while (level < cap && exp >= ExpFor(art, level + 1) && (!IsFormGate(level + 1) || passed >= level + 1)) level++;
            return level;
        }

        // 초식 연마를 기다리는 관문. 없으면 0.
        public int FormGate(MartialArt art)
        {
            int next = Mastery(art) + 1;
            return IsFormGate(next) && next <= CapOf(art) && ExpOf(art) >= ExpFor(art, next) ? next : 0;
        }

        public void PassForm(MartialArt art)
        {
            Forged[art] = FormGate(art);
            if (art.ascendsTo != null && Mastery(art) == 10) Transcend(art);
        }

        // 10성을 찍은 기본 무공은 다른 무공이 된다. 쓰던 칸과 10성을 그대로 잇는다.
        void Transcend(MartialArt art)
        {
            var next = art.ascendsTo;
            if (ActiveMind == art) activeMind = next;
            Arts[Arts.IndexOf(art)] = next;
            masteryExp.Remove(art);
            Forged.Remove(art);
            masteryExp[next] = ExpFor(next, 10);
            Forged[next] = 10;
        }

        // 개발 패널: level성으로 맞추고 경험치는 다음 성까지 채운다. 다음 성이 관문이면 연마가 열린다.
        internal void SetMasteryForTest(MartialArt art, int level)
        {
            if (!Arts.Contains(art)) Arts.Add(art);
            MasteryCaps.Remove(art);
            masteryExp[art] = ExpFor(art, level + 1);
            Forged[art] = new[] { 6, 9, 10 }.LastOrDefault(g => g <= level);
        }

        // 초식 연마 한 판의 결과. 피로는 언제나 든다.
        public void ApplyForm(FormPractice p)
        {
            Fatigue += FatigueCost(Training.Sparring);
            if (p.Passed) PassForm(p.Art);
        }

        public void GainMastery(MartialArt art, int exp) =>
            masteryExp[art] = (masteryExp.TryGetValue(art, out var e) ? e : 0) + exp * (100 + Stats.Wis * 5) / 100;

        public int Skill(SkillKind k) => SkillLevels.TryGetValue(k, out var v) ? v : 0;

        // 기예는 쓸수록 오른다. 한 번에 진척 10(오성으로 늘어난다), 다음 수치까지 100 + 수치×10.
        public void GainSkill(SkillKind k, int uses)
        {
            if (k == SkillKind.None) return;
            int level = Skill(k);
            int progress = (SkillProgress.TryGetValue(k, out var p) ? p : 0) + uses * 10 * (100 + Stats.Wis * 5) / 100;
            while (level < MaxSkill && progress >= 100 + level * 10) progress -= 100 + level++ * 10;
            SkillLevels[k] = level;
            SkillProgress[k] = progress;
        }

        static int FatigueCost(Training t) => t switch
        {
            Training.Tonap => 20,
            Training.Reading => 15,
            _ => 25,
        };

        public bool CanTrain(Training t) => Fatigue + FatigueCost(t) <= MaxFatigue;
        public int Efficiency => Fatigue >= MaxFatigue * 8 / 10 ? 50 : 100; // 지치면 수련 효과가 절반
        public int TonapGain => (10 + Stats.Wis / 2) * Efficiency / 100;    // 자동 수련의 내공 축적량

        // 수련 한 번. 피로가 80%를 넘은 상태면 효과가 절반. 대련에는 단련할 무공이 필요하다.
        public bool Train(Training t, MartialArt sparArt = null)
        {
            if (!CanTrain(t) || (t == Training.Sparring && !Arts.Contains(sparArt))) return false;
            int eff = Efficiency, tonap = TonapGain;
            Fatigue += FatigueCost(t);

            switch (t)
            {
                case Training.Tonap:
                    InnerPower = Math.Min(InnerPowerToBreak, InnerPower + tonap);
                    TrainInnerArt(tonap);
                    Raise(StatKind.Inner, 15 * eff / 100);
                    break;
                case Training.Sparring:
                    GainMastery(sparArt, 30 * eff / 100);
                    GainSkill(sparArt.skill, 3);
                    Raise(StatKind.Outer, 15 * eff / 100);
                    Raise(StatKind.Light, 15 * eff / 100);
                    break;
                case Training.Reading:
                    Raise(StatKind.Wis, 25 * eff / 100);
                    break;
                case Training.Labor:
                    Raise(StatKind.Con, 20 * eff / 100);
                    Raise(StatKind.Outer, 10 * eff / 100);
                    Money += 10;
                    break;
            }
            return true;
        }

        // 일주천 한 판의 결과. 피로는 언제나 든다. 주화입마면 내상. 얻은 내공 축적량을 돌려준다.
        // site: 폭포·바위·절벽 같은 수련터면 내공 스탯 대신 그 스탯이 곱절로 오른다.
        public int ApplyCirculation(Circulation c, StatKind? site = null)
        {
            int gain = (int)(TonapGain * c.Reward), inner = (int)(15 * Efficiency / 100 * c.Reward);
            Fatigue += FatigueCost(Training.Tonap);
            if (c.Result == CirculationEnd.Overheated) InnerInjured = true;
            InnerPower = Math.Min(InnerPowerToBreak, InnerPower + gain);
            Raise(site ?? StatKind.Inner, site != null ? inner * 2 : inner);
            if (site != null && c.Result == CirculationEnd.Complete) GainSkill(SiteSkill(site.Value), 5);
            TrainInnerArt(gain);
            return gain;
        }

        // 맨몸 수련터의 기예: 폭포는 장법, 바위는 권법, 절벽은 각법.
        static SkillKind SiteSkill(StatKind site) => site switch
        {
            StatKind.Con => SkillKind.Palm,
            StatKind.Outer => SkillKind.Fist,
            StatKind.Light => SkillKind.Kick,
            _ => SkillKind.None,
        };

        // 일주천은 운용 심법을 단련한다. 얻은 내공 축적량만큼 숙련이 오른다.
        void TrainInnerArt(int amount)
        {
            if (ActiveMind != null) GainMastery(ActiveMind, amount);
        }

        // 스탯은 진척도가 100 쌓일 때마다 1 오른다. 경지에 따른 상한이 있다.
        void Raise(StatKind s, int amount)
        {
            ref int p = ref statProgress[(int)s];
            p += amount;
            for (; p >= 100; p -= 100)
            {
                ref int stat = ref Stats.Ref(s);
                if (stat < StatCap) stat++;
            }
        }

        // 휴식: 피로를 풀고 내상을 다스린다. 객잔은 돈이 들고 소속 문파 거처는 무료.
        public bool Rest(bool atSect)
        {
            if (!atSect)
            {
                if (Money < InnCost) return false;
                Money -= InnCost;
            }
            Fatigue = 0;
            InnerInjured = false;
            return true;
        }

        bool CanAscend => Realm != Realm.Hwagyeong && (!IsWall(Realm) || Enlightened);
        public bool CanAttemptBreakthrough => CanAscend && InnerPower >= InnerPowerToBreak;

        // 폐관수련의 심마: 주인공의 무공과 장비를 그대로 쓰는 그림자. 이 값이 스탯 배율이다.
        // 오성이 높고 수련동(bonus)이 있으면 약하고, 내상이 있으면 세다. 0.78~1.22
        public float DemonStrength(float bonus = 0) =>
            1.25f - 0.5f * Math.Clamp(0.4f + Stats.Wis * 0.02f + bonus - (InnerInjured ? 0.2f : 0), 0.05f, 0.95f);

        public Combatant InnerDemon(float bonus = 0)
        {
            float strength = DemonStrength(bonus);
            var geared = GearedStats;
            var s = new Stats();
            foreach (StatKind k in Enum.GetValues(typeof(StatKind))) s.Ref(k) = (int)Math.Round(geared.Ref(k) * strength);
            s.Light = geared.Light; // 경공은 그대로: 1만 달라도 행동 순서가 뒤집혀 승패가 갈린다
            return new Combatant("심마", false, s, BattleArts, Row.Front, false, (int)Realm, Arts.ToDictionary(a => a, Mastery))
            {
                Sprite = CharacterCreation.Looks[Appearance], Figure = $"심마{Appearance + 1}", Gear = Worn.ToList(), Mind = ActiveMind, Nature = ActiveMind != null ? ActiveMind.nature : InnerNature.None, Skills = new Dictionary<SkillKind, int>(SkillLevels),
            };
        }

        // 심마와 싸운 결과. 지면 내상을 입고 내공 20%를 잃는다.
        public void FinishBreakthrough(bool won)
        {
            if (won)
            {
                Ascend();
                return;
            }
            InnerInjured = true;
            InnerPower = InnerPower * 8 / 10;
        }

        void Ascend()
        {
            Realm++;
            InnerPower = 0;
            Enlightened = false;
        }

        public IEnumerable<ItemData> Worn => Equipped.Where(i => i != null);

        // 장비까지 더한 스탯. 전투, 상점 값, 전리품에 쓴다. 수련과 경지 상한은 맨몸 Stats를 본다.
        public Stats GearedStats
        {
            get
            {
                var s = new Stats();
                foreach (StatKind k in Enum.GetValues(typeof(StatKind))) s.Ref(k) = Stats.Ref(k) + Worn.Sum(i => i.stats.Ref(k));
                return s;
            }
        }

        // 가방에서 꺼내 입는다. 장신구는 빈 칸부터 채우고, 입던 것은 가방으로 돌아간다.
        public bool Equip(ItemData item)
        {
            if (!item.IsGear || !Bag.Remove(item)) return false;
            int slot = (int)item.kind;
            if (item.kind == ItemKind.Trinket && Equipped[slot] != null && Equipped[slot + 1] == null) slot++;
            Unequip(slot);
            Equipped[slot] = item;
            return true;
        }

        public void Unequip(int slot)
        {
            if (Equipped[slot] != null) Bag.Add(Equipped[slot]);
            Equipped[slot] = null;
        }

        // 인덕 1당 2% 싸게 산다 (최대 30%). 파는 값은 기본 값의 절반.
        public int BuyPrice(ItemData item) => item.price * (100 - Math.Min(30, GearedStats.Virtue * 2)) / 100;
        public static int SellPrice(ItemData item) => item.price / 2;

        public bool Buy(ItemData item)
        {
            if (Money < BuyPrice(item)) return false;
            Money -= BuyPrice(item);
            Bag.Add(item);
            return true;
        }

        public bool Sell(ItemData item)
        {
            if (item.price == 0 || !Bag.Remove(item)) return false;
            Money += SellPrice(item);
            return true;
        }

        static bool RaisesStats(ItemData item) => item.kind == ItemKind.Elixir && CharacterCreation.All.Any(k => item.stats.Ref(k) != 0);

        // 전투 밖에서 약 먹기: 피로, 내공, 내상, 영약의 스탯·돌파에 듣는 것만.
        public bool CanUse(ItemData item) =>
            !item.IsGear && Bag.Count(item) > 0 && (RaisesStats(item) || (item.breakthrough && CanAscend)
                || (item.fatigue > 0 && Fatigue > 0) || (item.innerPower > 0 && InnerPower < InnerPowerToBreak)
                || (InnerInjured && item.cures.Contains(StatusKind.InnerInjury)));

        public bool Use(ItemData item)
        {
            if (!CanUse(item)) return false;
            Bag.Remove(item);
            Fatigue = Math.Max(0, Fatigue - item.fatigue);
            InnerPower = Math.Min(InnerPowerToBreak, InnerPower + item.innerPower);
            if (item.cures.Contains(StatusKind.InnerInjury)) InnerInjured = false;
            // ponytail: 영약은 경지 상한을 넘는다. 무한히 나오는 영약이 늘면 상한을 건다
            if (item.kind == ItemKind.Elixir)
                foreach (var k in CharacterCreation.All) Stats.Ref(k) += item.stats.Ref(k);
            if (item.breakthrough) Ascend();
            return true;
        }

        public Combatant ToCombatant(Row row = Row.Front) =>
            new(Name, true, GearedStats, BattleArts, row, InnerInjured, (int)Realm, Arts.ToDictionary(a => a, Mastery))
            {
                Sprite = CharacterCreation.Looks[Appearance], Figure = $"주인공{Appearance + 1}", Gear = Worn.ToList(), Mind = ActiveMind, Nature = ActiveMind != null ? ActiveMind.nature : InnerNature.None, Skills = new Dictionary<SkillKind, int>(SkillLevels),
            };

        // Battle.Finish() 뒤에 부른다. 내상을 이어받고, 쓴 무공의 숙련도를 올린다.
        public void ApplyBattle(Combatant c)
        {
            InnerInjured = c.InnerInjured;
            foreach (var kv in c.UseCounts) GainMastery(kv.Key, kv.Value * 10);
            foreach (var kv in c.SkillUses) GainSkill(kv.Key, kv.Value);
        }
    }
}
