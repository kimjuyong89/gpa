using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 전진검법·전진심법을 끝까지 파면 만류귀종이 된다.
    public class UltimateTests
    {
        const string Dir = "Assets/_Project/Data/MartialArts/";
        static MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"{Dir}{file}.asset");
        static MartialArt Sword => Art("Jeonjingeom");
        static MartialArt Mind => Art("Jeonjinsim");

        static Character Hero() => new("나", new Stats { Wis = 5 }, new[] { Sword, Mind });

        static void PassAll(Character h, MartialArt art)
        {
            h.GainMastery(art, 1000000);
            while (h.FormGate(art) > 0) h.PassForm(art);
        }

        [Test]
        public void BasicArtsAreNamedAndLinked()
        {
            Assert.AreEqual("전진검법", Sword.displayName);
            Assert.AreEqual("전진심법", Mind.displayName);
            Assert.AreEqual("만류귀종(검)", Sword.ascendsTo.displayName);
            Assert.AreEqual("만류귀종(심)", Mind.ascendsTo.displayName);
            Assert.AreEqual(WeaponType.Sword, Sword.weapon);
            Assert.IsNotEmpty(Mind.forms);
        }

        [Test]
        public void LatePlainArtsNeedFarMoreExperience()
        {
            var plain = ScriptableObject.CreateInstance<MartialArt>();
            plain.kind = ArtKind.Outer;
            var h = new Character("나", new Stats(), new[] { plain, Sword });
            h.GainMastery(plain, 2250);
            h.GainMastery(Sword, 2250);
            Assert.Greater(h.FormGate(plain), 0, "일반 무공은 6성 관문이 열린다");
            Assert.AreEqual(0, h.FormGate(Sword), "같은 경험치로 전진검법은 6성 관문도 열리지 않는다");
            Assert.AreEqual(5, h.Mastery(Sword));

            var early = new Character("나", new Stats(), new[] { Sword });
            early.GainMastery(Sword, 150);
            Assert.AreEqual(3, early.Mastery(Sword), "초반은 다른 무공과 같다");
        }

        [Test]
        public void TenthStageChangesTheArtForGood()
        {
            var h = Hero();
            PassAll(h, Sword);
            Assert.IsFalse(h.Arts.Contains(Sword));
            var ultimate = h.Arts.Find(a => a.displayName == "만류귀종(검)");
            Assert.IsNotNull(ultimate);
            Assert.AreEqual(10, h.Mastery(ultimate));
            Assert.AreEqual(0, h.FormGate(ultimate));

            PassAll(h, Mind);
            Assert.IsNotNull(h.Arts.Find(a => a.displayName == "만류귀종(심)"));
            Assert.AreEqual(2, h.Arts.Count);
        }

        [Test]
        public void NinthStageDoesNotChangeTheArt()
        {
            var h = Hero();
            h.GainMastery(Sword, 1000000);
            h.PassForm(Sword);
            h.PassForm(Sword);
            Assert.AreEqual(9, h.Mastery(Sword));
            Assert.IsTrue(h.Arts.Contains(Sword));
        }

        [Test]
        public void FinalGateIsHarderThanAnyOther()
        {
            FormPractice normal = new(Art("CheongunKwaegeom"), 10, 5, 0), final = new(Sword, 10, 5, 0), nine = new(Sword, 9, 5, 0);
            Assert.AreEqual(0.9f, final.Need, 0.001f);
            Assert.Greater(final.Notes.Count, normal.Notes.Count);
            Assert.AreEqual(nine.Need, new FormPractice(Art("CheongunKwaegeom"), 9, 5, 0).Need, "9성은 그대로다");
        }

        [Test]
        public void SwordIsTheStrongestArtInTheGame()
        {
            var ultimate = Sword.ascendsTo;
            var others = AssetDatabase.FindAssets("t:MartialArt", new[] { Dir.TrimEnd('/') })
                .Select(g => AssetDatabase.LoadAssetAtPath<MartialArt>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a.kind == ArtKind.Outer && a != ultimate).ToList();
            Assert.IsNotEmpty(others);
            foreach (var a in others) Assert.Greater(ultimate.power * ultimate.hits, a.power * a.hits, a.displayName);
            Assert.AreEqual(Affinity.None, ultimate.affinity, "상성을 타지 않는다");
            Assert.IsTrue(ultimate.reachesBackRow);
        }

        [Test]
        public void MindGuardsAgainstEveryStatus()
        {
            var ultimate = Mind.ascendsTo;
            var h = new Character("나", new Stats(), new[] { ultimate });
            var c = h.ToCombatant();
            foreach (var k in new[] { StatusKind.Bleed, StatusKind.Poison, StatusKind.Pressure, StatusKind.InnerInjury, StatusKind.QiDeviation })
                Assert.IsTrue(c.Resists(k), k.ToString());
            Assert.Greater(ultimate.maxInnerBonus, Art("Cheongsimgyeol").maxInnerBonus);
        }

        [Test]
        public void CirculationTrainsTheInnerArt()
        {
            var h = Hero();
            for (int i = 0; i < 5; i++)
            {
                h.Train(Training.Tonap);
                h.Fatigue = 0;
            }
            Assert.AreEqual(2, h.Mastery(Mind), "일주천으로 운용 중인 전진심법이 는다");
        }
    }
}
