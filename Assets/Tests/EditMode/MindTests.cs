using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 심법은 하나만 운용하고, 무공 계열과 맞는지에 따라 위력과 위험이 달라진다.
    public class MindTests
    {
        static MartialArt Outer(Lineage line)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 10;
            a.lineage = line;
            return a;
        }

        static MartialArt Mind(Lineage line, int bonus = 0)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Mind;
            a.lineage = line;
            a.maxInnerBonus = bonus;
            return a;
        }

        static Combatant With(MartialArt mind, MartialArt art) => new("가", true, new Stats(), new[] { art }) { Mind = mind };

        [TestCase(Lineage.Common, Lineage.Common, MindFit.Common)]
        [TestCase(Lineage.Orthodox, Lineage.Common, MindFit.Common)]
        [TestCase(Lineage.Orthodox, Lineage.Orthodox, MindFit.Match)]
        [TestCase(Lineage.Deviant, Lineage.Deviant, MindFit.Match)]
        [TestCase(Lineage.Common, Lineage.Orthodox, MindFit.Weak)]
        [TestCase(Lineage.Common, Lineage.Deviant, MindFit.Weak)]
        [TestCase(Lineage.Orthodox, Lineage.Deviant, MindFit.Clash)]
        [TestCase(Lineage.Deviant, Lineage.Orthodox, MindFit.Clash)]
        [TestCase(Lineage.Universal, Lineage.Common, MindFit.Match)]
        [TestCase(Lineage.Universal, Lineage.Deviant, MindFit.Match)]
        public void FitDependsOnMindAndArt(Lineage mind, Lineage art, MindFit fit)
        {
            var a = Outer(art);
            Assert.AreEqual(fit, With(Mind(mind), a).FitOf(a));
        }

        [Test]
        public void FitChangesPower()
        {
            float Mult(Lineage mind, Lineage art)
            {
                var a = Outer(art);
                return With(Mind(mind), a).MindMultiplier(a);
            }
            Assert.AreEqual(1f, Mult(Lineage.Common, Lineage.Common), 0.001f);
            Assert.Greater(Mult(Lineage.Orthodox, Lineage.Orthodox), 1f);
            Assert.Less(Mult(Lineage.Common, Lineage.Orthodox), 1f);
            Assert.Less(Mult(Lineage.Deviant, Lineage.Orthodox), Mult(Lineage.Common, Lineage.Orthodox));
        }

        [Test]
        public void UnitsWithoutARuleFightAtFullPower()
        {
            var a = Outer(Lineage.Deviant);
            var enemy = new Combatant("적", false, new Stats(), new[] { a });
            Assert.AreEqual(MindFit.Common, enemy.FitOf(a));
            Assert.AreEqual(1f, enemy.MindMultiplier(a));
        }

        [Test]
        public void ClashingArtCanBringQiDeviation()
        {
            var a = Outer(Lineage.Deviant);
            int hit = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var hero = new Combatant("가", true, new Stats { Light = 10, Con = 50 }, new[] { a }) { Mind = Mind(Lineage.Orthodox) };
                var foe = new Combatant("적", false, new Stats { Con = 50 }, new[] { a });
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(seed));
                b.BeginTurn();
                b.UseArt(a, foe);
                if (hero.Has(StatusKind.QiDeviation)) hit++;
            }
            Assert.That(hit, Is.InRange(25, 70), "약 15%");
        }

        [Test]
        public void MatchingArtNeverBringsQiDeviation()
        {
            var a = Outer(Lineage.Orthodox);
            for (int seed = 0; seed < 100; seed++)
            {
                var hero = new Combatant("가", true, new Stats { Light = 10, Con = 50 }, new[] { a }) { Mind = Mind(Lineage.Orthodox) };
                var foe = new Combatant("적", false, new Stats { Con = 50 }, new[] { a });
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(seed));
                b.BeginTurn();
                b.UseArt(a, foe);
                Assert.IsFalse(hero.Has(StatusKind.QiDeviation));
            }
        }

        [Test]
        public void OnlyTheActiveMindCountsInBattle()
        {
            MartialArt first = Mind(Lineage.Common, 10), second = Mind(Lineage.Orthodox, 30);
            var h = new Character("나", new Stats(), new[] { first, second });
            Assert.AreSame(first, h.ActiveMind, "처음엔 먼저 배운 심법");
            Assert.AreEqual(20 + 10, h.ToCombatant().MaxInner);

            h.ActiveMind = second;
            var c = h.ToCombatant();
            Assert.AreEqual(20 + 30, c.MaxInner);
            Assert.AreSame(second, c.Mind);
            Assert.AreEqual(1, c.Passives.Count());
        }

        [Test]
        public void DemonUsesTheSameMindAsTheHero()
        {
            MartialArt first = Mind(Lineage.Common, 10), second = Mind(Lineage.Orthodox, 30);
            var h = new Character("나", new Stats(), new[] { first, second }) { ActiveMind = second };
            var d = h.InnerDemon();
            Assert.AreSame(second, d.Mind);
            Assert.AreEqual(1, d.Passives.Count());
        }

        [Test]
        public void OnlyTheActiveMindGrowsFromCirculation()
        {
            MartialArt first = Mind(Lineage.Common), second = Mind(Lineage.Orthodox);
            var h = new Character("나", new Stats(), new[] { first, second }) { ActiveMind = second };
            for (int i = 0; i < 6; i++)
            {
                h.Train(Training.Tonap);
                h.Fatigue = 0;
            }
            Assert.AreEqual(1, h.Mastery(first));
            Assert.AreEqual(2, h.Mastery(second));
        }

        [Test]
        public void ChoiceSurvivesSaveAndTranscendence()
        {
            MartialArt first = Mind(Lineage.Common), second = Mind(Lineage.Orthodox);
            first.name = "첫째";
            second.name = "둘째";
            var state = new GameState(new Character("나", new Stats(), new[] { first, second }) { ActiveMind = second });
            var loaded = new GameState(new Character("?", new Stats()));
            SaveGame.Read(SaveGame.Write(state, "{}"), loaded, n => n == "첫째" ? first : n == "둘째" ? second : null);
            Assert.AreSame(second, loaded.Hero.ActiveMind);

            var ultimate = Mind(Lineage.Universal);
            first.ascendsTo = ultimate;
            var h = new Character("나", new Stats(), new[] { first }) { ActiveMind = first };
            h.GainMastery(first, 1000000);
            while (h.FormGate(first) > 0) h.PassForm(first);
            Assert.AreSame(ultimate, h.ActiveMind, "만류귀종(심)이 이어받는다");
        }

        [Test]
        public void ArtDataHasLineages()
        {
            MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"Assets/_Project/Data/MartialArts/{file}.asset");
            Assert.AreEqual(Lineage.Common, Art("Jeonjingeom").lineage);
            Assert.AreEqual(Lineage.Common, Art("Jeonjinsim").lineage);
            Assert.AreEqual(Lineage.Orthodox, Art("CheongunKwaegeom").lineage);
            Assert.AreEqual(Lineage.Orthodox, Art("Cheongsimgyeol").lineage);
            Assert.AreEqual(Lineage.Deviant, Art("HeuksaBisu").lineage);
            Assert.AreEqual(Lineage.Deviant, Art("Eumsagong").lineage);
            Assert.AreEqual(Lineage.Universal, Art("ManryuSim").lineage);
            Assert.IsNotEmpty(Art("Cheongsimgyeol").forms);
            Assert.IsNotEmpty(Art("Eumsagong").forms);
        }
    }
}
