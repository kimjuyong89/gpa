using System;
using System.Collections.Generic;
using System.Linq;

namespace GPA.Combat
{
    public enum Row { Front, Back }

    [Serializable]
    public class Stats
    {
        public int Str, Agi, Con, Wis, Virtue, Luck; // 근력, 민첩, 근골, 오성, 인덕, 기연
    }

    public class Status
    {
        public StatusKind Kind;
        public int Turns; // 음수면 전투 내내
    }

    // 전투에 참가한 한 명. 수치 공식은 GDD의 임시값.
    public class Combatant
    {
        public readonly string Name;
        public readonly bool IsAlly;
        public readonly Stats Stats;
        public readonly IReadOnlyList<MartialArt> Arts;
        public Row Row;
        public int Hp, Inner;
        public Affinity Stance; // 마지막으로 쓴 외공의 속성. 맞을 때 상성 판정에 쓰인다
        public bool InnerInjured { get; private set; } // 내상: 전투가 끝나도 남는다
        public double NextTurnAt;
        public readonly List<Status> Statuses = new();
        public int SpeedBuff, EvasionBuff, BuffTurns;
        public bool Counter;
        readonly Dictionary<MartialArt, int> cooldowns = new();

        public Combatant(string name, bool isAlly, Stats stats, IEnumerable<MartialArt> arts, Row row = Row.Front, bool innerInjured = false)
        {
            Name = name;
            IsAlly = isAlly;
            Stats = stats;
            Arts = arts.ToList();
            Row = row;
            InnerInjured = innerInjured;
            Hp = MaxHp;
            Inner = MaxInner;
        }

        public IEnumerable<MartialArt> Passives => Arts.Where(a => a.kind is ArtKind.Mind or ArtKind.Inner);
        public int MaxHp => 50 + Stats.Con * 10;
        public int MaxInner => (int)((20 + Stats.Con * 5 + Passives.Sum(a => a.maxInnerBonus)) * (InnerInjured ? 0.7f : 1f));
        public int Speed => Stats.Agi + SpeedBuff;
        public float EvasionChance => Math.Min(0.5f, (Stats.Agi + EvasionBuff) * 0.005f);
        public float CritChance => Stats.Luck * 0.01f;
        public bool IsDown => Hp <= 0;

        public bool Has(StatusKind k) => Statuses.Any(s => s.Kind == k);
        public bool Resists(StatusKind k) => Passives.Any(a => a.resists.Contains(k));
        public int Cooldown(MartialArt art) => cooldowns.TryGetValue(art, out var t) ? t : 0;
        // 자기 턴 시작마다 1씩 줄어서 0이 되면 쓸 수 있다. +1은 사용한 턴 다음부터 세기 위해서.
        internal void StartCooldown(MartialArt art) => cooldowns[art] = art.cooldown + 1;

        public void TakeDamage(int dmg) => Hp = Math.Max(0, Hp - dmg);

        // 자기 턴 시작 처리. 행동할 수 없으면 true.
        internal bool StartTurn(List<string> log)
        {
            bool pressured = Has(StatusKind.Pressure);
            foreach (var s in Statuses.ToList())
            {
                int pct = s.Kind switch { StatusKind.Bleed => 8, StatusKind.Poison => 8, StatusKind.QiDeviation => 5, _ => 0 };
                if (pct > 0)
                {
                    int dmg = Math.Max(1, MaxHp * pct / 100);
                    TakeDamage(dmg);
                    log.Add($"{Name}: {Label(s.Kind)}으로 체력 {dmg} 감소");
                }
                if (s.Turns > 0 && --s.Turns == 0) Statuses.Remove(s);
            }

            Inner = Math.Min(MaxInner, Inner + MaxInner * 5 / 100);
            foreach (var art in cooldowns.Keys.ToList())
                if (cooldowns[art] > 0) cooldowns[art]--;
            if (BuffTurns > 0 && --BuffTurns == 0)
            {
                SpeedBuff = EvasionBuff = 0;
                Counter = false;
            }

            if (pressured) log.Add($"{Name}: 점혈되어 움직이지 못한다");
            return pressured;
        }

        internal void Inflict(StatusKind k, int turns, float chance, Random rng, List<string> log)
        {
            if (k == StatusKind.None) return;
            if (Resists(k)) chance /= 2;
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
            log.Add($"{Name}: {Label(k)}");
        }

        // 전투 종료: 주화입마는 내상으로 바뀌고 나머지 상태는 사라진다.
        internal void EndBattle()
        {
            if (Has(StatusKind.QiDeviation)) InnerInjured = true;
            Statuses.Clear();
            cooldowns.Clear();
            SpeedBuff = EvasionBuff = BuffTurns = 0;
            Counter = false;
            Inner = Math.Min(Inner, MaxInner);
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
