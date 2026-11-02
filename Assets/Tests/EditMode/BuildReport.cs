using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GPA.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 빌드별 자동 검증. 무공마다 맞는 심법과 문파 규칙을 붙여, 같은 스탯의 기준 상대와 자동 전투를 돌린다.
    // 결과는 docs/17_밸런스표.md에 쓴다. 기준 상대는 강·유·쾌 셋이고, 승률은 셋의 평균이다.
    public class BuildReport
    {
        const int Runs = 200;
        const string Dir = "Assets/_Project/Data/";

        class Tier
        {
            public string Name;
            public int Realm, Mastery, Skill, Bonus, RivalPower;
        }

        static readonly Tier[] Tiers =
        {
            new() { Name = "초반", Realm = 0, Mastery = 1, Skill = 0, Bonus = 0, RivalPower = 8 },
            new() { Name = "중반", Realm = 1, Mastery = 6, Skill = 10, Bonus = 6, RivalPower = 13 },
            new() { Name = "후반", Realm = 2, Mastery = 9, Skill = 25, Bonus = 14, RivalPower = 18 },
        };

        static MartialArt Load(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"{Dir}MartialArts/{file}.asset");

        static List<MartialArt> OuterArts => AssetDatabase.FindAssets("t:MartialArt", new[] { Dir + "MartialArts", Dir + "Resources/Arts" })
            .Select(g => AssetDatabase.LoadAssetAtPath<MartialArt>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a.kind == ArtKind.Outer && a.name != "Claw")
            .OrderBy(a => a.grade).ThenBy(a => a.skill).ThenBy(a => a.displayName).ToList();

        static Stats StatsOf(Tier t) =>
            new() { Outer = 5 + t.Bonus, Inner = 3 + t.Bonus, Light = 8 + t.Bonus, Con = 6 + t.Bonus, Wis = 5, Virtue = 5, Luck = 4 };

        static Combatant Hero(MartialArt art, MartialArt mind, Tier t, SectTrait trait)
        {
            var arts = new List<MartialArt> { art };
            if (mind != null) arts.Add(mind);
            return new Combatant("가", true, StatsOf(t), arts, Row.Front, false, t.Realm, arts.ToDictionary(a => a, _ => t.Mastery))
            {
                Mind = mind, Trait = trait,
                Skills = new Dictionary<SkillKind, int> { [art.skill] = t.Skill },
            };
        }

        // 상성에 치우치지 않도록 세 속성의 기준 초식을 돌아가며 상대한다. 위력은 같다.
        static MartialArt Basic(Affinity affinity, int power)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.displayName = "기준";
            a.affinity = affinity;
            a.power = power;
            return a;
        }

        static readonly Affinity[] Kinds = { Affinity.Gang, Affinity.Yu, Affinity.Kwae };

        static Combatant Foe(MartialArt art, Tier t, float strength, int lane)
        {
            var s = StatsOf(t);
            s.Outer = (int)(s.Outer * strength);
            s.Light = (int)(s.Light * strength);
            s.Con = (int)(s.Con * strength);
            s.Inner = (int)(s.Inner * strength);
            return new Combatant("적", false, s, new[] { art }, Row.Front, false, t.Realm, new Dictionary<MartialArt, int> { [art] = t.Mastery })
            {
                Temper = Temper.Aggressive, Lane = lane,
            };
        }

        static MartialArt MindFor(MartialArt art) => art.lineage switch
        {
            Lineage.Orthodox => Load("Cheongsimgyeol"),
            Lineage.Deviant => Load("Eumsagong"),
            _ => Load("Jeonjinsim"),
        };

        static SectTrait TraitFor(MartialArt art) => art.lineage switch
        {
            Lineage.Orthodox => SectTrait.Unbroken,
            Lineage.Deviant => SectTrait.Predator,
            _ => SectTrait.None,
        };

        static float Rate(MartialArt art, MartialArt mind, Tier t, SectTrait trait, int foes, float strength)
        {
            var rng = new System.Random(1);
            int wins = 0;
            for (int i = 0; i < Runs; i++)
            {
                var hero = Hero(art, mind, t, trait);
                var rival = Basic(Kinds[i % Kinds.Length], t.RivalPower);
                var enemies = Enumerable.Range(0, foes).Select(l => Foe(rival, t, strength, l));
                var b = new Battle(new[] { hero }, enemies, new System.Random(rng.Next()));
                for (int turns = 0; b.BeginTurn() != null && turns < 400; turns++) b.AutoAct();
                if (b.Outcome == Outcome.Victory) wins++;
            }
            return wins / (float)Runs;
        }

        static bool Usable(MartialArt art, Tier t) => art.grade == ArtGrade.Supreme || t.Realm >= (int)art.grade;

        const float Strong = 0.9f, Weak = 0.2f, Solo = 1f, Group = 0.4f;

        [Test]
        public void WriteBalanceTable()
        {
            var md = new StringBuilder();
            md.AppendLine("# 밸런스표");
            md.AppendLine();
            md.AppendLine("`Assets/Tests/EditMode/BuildReport.cs`가 만드는 자동 생성 문서다. 손으로 고치지 않는다.");
            md.AppendLine();
            md.AppendLine($"무공마다 맞는 심법과 문파 규칙을 붙이고, 같은 스탯의 강·유·쾌 기준 상대(위력 8·13·18)와 자동 전투 {Runs}회를 돌렸다. 1:3은 스탯 {Group:P0}의 상대 셋이다.");
            md.AppendLine();
            md.AppendLine("| 무공 | 등급 | 계통 | 초반 1:1 | 초반 1:3 | 중반 1:1 | 중반 1:3 | 후반 1:1 | 후반 1:3 |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");

            var strong = new List<string>();
            var weak = new List<string>();
            var rates = new Dictionary<MartialArt, List<float>>();
            foreach (var art in OuterArts)
            {
                var row = new List<string>();
                rates[art] = new List<float>();
                foreach (var t in Tiers)
                foreach (var (foes, strength, label) in new[] { (1, Solo, "1:1"), (3, Group, "1:3") })
                {
                    if (!Usable(art, t))
                    {
                        row.Add("–");
                        continue;
                    }
                    float r = Rate(art, MindFor(art), t, TraitFor(art), foes, strength);
                    row.Add($"{r:P0}");
                    rates[art].Add(r);
                    if (r >= Strong) strong.Add($"{art.displayName} {t.Name} {label} {r:P0}");
                    if (r <= Weak) weak.Add($"{art.displayName} {t.Name} {label} {r:P0}");
                }
                string moves = art.moves.Length > 0 ? "" : " (초식 없음)";
                md.AppendLine($"| {art.displayName}{moves} | {art.grade} | {Combatant.SkillLabel(art.skill)} | {string.Join(" | ", row)} |");
            }

            md.AppendLine();
            md.AppendLine($"## 강함 ({Strong:P0} 이상)");
            md.AppendLine();
            foreach (var s in strong) md.AppendLine("- " + s);
            md.AppendLine();
            md.AppendLine($"## 약함 ({Weak:P0} 이하)");
            md.AppendLine();
            foreach (var s in weak) md.AppendLine("- " + s);

            md.AppendLine();
            md.AppendLine("## 심법과 문파 규칙");
            md.AppendLine();
            md.AppendLine("중반 1:1 승률이다.");
            md.AppendLine();
            md.AppendLine("| 무공 | 전진심법 | 맞는 심법 | 맞는 심법 + 문파 규칙 |");
            md.AppendLine("|---|---|---|---|");
            var mid = Tiers[1];
            foreach (var name in new[] { "CheongunKwaegeom", "HeuksaBisu" })
            {
                var art = Load(name);
                md.AppendLine($"| {art.displayName} | {Rate(art, Load("Jeonjinsim"), mid, SectTrait.None, 1, Solo):P0} | {Rate(art, MindFor(art), mid, SectTrait.None, 1, Solo):P0} | {Rate(art, MindFor(art), mid, TraitFor(art), 1, Solo):P0} |");
            }

            File.WriteAllText("docs/17_밸런스표.md", md.ToString());
            Debug.Log("[BUILD]\n" + md);

            // 이류는 초반에 못 쓰므로 등급 비교는 중·후반만 본다
            static float Late(List<float> v) => v.Skip(v.Count - 4).Average();

            // 전진검법은 만류귀종이 되기 전까지 초식 있는 삼류 무공 가운데 가장 약하다
            var jeonjin = Load("Jeonjingeom");
            var others = rates.Where(kv => kv.Key.grade == ArtGrade.Third && kv.Key.moves.Length > 0 && kv.Key != jeonjin && kv.Key.name != "Tuseok");
            Assert.Less(Late(rates[jeonjin]), others.Min(kv => Late(kv.Value)));

            // 등급이 높을수록 강하다
            float Grade(ArtGrade g) => rates.Where(kv => kv.Key.grade == g).SelectMany(kv => kv.Value.Skip(kv.Value.Count - 4)).Average();
            Assert.Less(Grade(ArtGrade.Third), Grade(ArtGrade.Second));
            Assert.LessOrEqual(Grade(ArtGrade.Second), Grade(ArtGrade.Supreme));

            // 쓸모없는 무공은 없다. 투석은 적 전용이고 전진검법은 일부러 약하다
            foreach (var kv in rates.Where(kv => kv.Key.name != "Tuseok" && kv.Key != jeonjin))
                Assert.Greater(kv.Value.Max(), Weak, kv.Key.displayName);
        }
    }
}
