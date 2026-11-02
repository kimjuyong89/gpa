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

        static Character Three(out MartialArt basic, out MartialArt pure, out MartialArt other)
        {
            basic = Mind(Lineage.Common);
            basic.keepsMastery = true;
            pure = Mind(Lineage.Orthodox);
            other = Mind(Lineage.Orthodox);
            var h = new Character("나", new Stats(), new[] { basic, pure, other });
            foreach (var a in new[] { basic, pure, other }) h.GainMastery(a, 200);
            return h;
        }

        [Test]
        public void SwitchingWipesOtherMindsButNotTheBasicOne()
        {
            var h = Three(out var basic, out var pure, out var other);
            Assert.AreEqual(3, h.Mastery(pure));
            Assert.IsTrue(h.SwitchMind(pure));
            Assert.AreSame(pure, h.ActiveMind);
            Assert.AreEqual(3, h.Mastery(pure), "새 심법은 그대로다");
            Assert.AreEqual(3, h.Mastery(basic), "전진심법은 남는다");
            Assert.AreEqual(1, h.Mastery(other), "다른 심법은 처음으로");
            Assert.IsTrue(h.Arts.Contains(other), "잊지는 않는다");
        }

        [Test]
        public void LeavingAMindWipesIt()
        {
            var h = Three(out var basic, out var pure, out _);
            h.SwitchMind(pure);
            h.SwitchMind(basic);
            Assert.AreEqual(1, h.Mastery(pure));
            Assert.AreEqual(3, h.Mastery(basic));
        }

        [Test]
        public void WipedFormGatesMustBePassedAgain()
        {
            var h = Three(out _, out var pure, out var other);
            h.GainMastery(pure, 100000);
            h.PassForm(pure);
            Assert.Greater(h.Mastery(pure), 5);
            h.SwitchMind(other);
            h.GainMastery(pure, 100000);
            Assert.AreEqual(5, h.Mastery(pure), "관문도 다시 넘어야 한다");
        }

        [Test]
        public void SwitchingToTheSameMindChangesNothing()
        {
            var h = Three(out var basic, out var pure, out _);
            Assert.AreSame(basic, h.ActiveMind);
            Assert.IsFalse(h.SwitchMind(basic));
            Assert.AreEqual(3, h.Mastery(pure));
        }

        [Test]
        public void ForbiddenSwitchCostsNothing()
        {
            var yang = Mind(Lineage.Orthodox);
            yang.nature = InnerNature.Yang;
            var yin = Mind(Lineage.Deviant);
            yin.nature = InnerNature.Yin;
            var h = new Character("나", new Stats(), new[] { yang, yin }) { ActiveMind = yang };
            h.GainMastery(yin, 200);
            Assert.IsFalse(h.SwitchMind(yin));
            Assert.AreSame(yang, h.ActiveMind);
            Assert.AreEqual(3, h.Mastery(yin));
        }

        [Test]
        public void LossListNamesOnlyMindsWithProgress()
        {
            var h = Three(out var basic, out var pure, out var other);
            CollectionAssert.AreEquivalent(new[] { other }, h.MindsLostBy(pure));
            var fresh = Mind(Lineage.Common);
            h.Arts.Add(fresh);
            CollectionAssert.AreEquivalent(new[] { other }, h.MindsLostBy(pure), "숙련이 없으면 잃을 것도 없다");
        }

        static MartialArt Natured(InnerNature nature)
        {
            var a = Mind(Lineage.Orthodox);
            a.nature = nature;
            return a;
        }

        static Character Pair(InnerNature from, InnerNature to, out MartialArt first, out MartialArt second)
        {
            first = Natured(from);
            second = Natured(to);
            var h = new Character("나", new Stats(), new[] { first, second }) { ActiveMind = first };
            h.GainMastery(first, 1000); // 관문을 넘으면 6성
            return h;
        }

        [TestCase(InnerNature.Yang)]
        [TestCase(InnerNature.Yin)]
        public void SameNatureCarriesEightyPercentAfterTheFirstStage(InnerNature nature)
        {
            var h = Pair(nature, nature, out var first, out var second);
            Assert.AreEqual(6, Character_Mastery(h, first));
            Assert.IsTrue(h.KeepsLegacy(second));
            h.SwitchMind(second);
            Assert.AreEqual(1, h.Mastery(second), "바로 넘어오지 않는다");
            Assert.AreEqual(1, h.Mastery(first), "쓰던 심법은 초기화된다");
            Assert.IsTrue(h.LegacyPending);

            h.GainMastery(second, 40);
            Assert.AreEqual(1, h.Mastery(second), "아직 첫 단계를 못 넘었다");
            Assert.IsTrue(h.LegacyPending);

            h.GainMastery(second, 10); // 50이면 2성. 800 + 50 = 850
            Assert.IsFalse(h.LegacyPending);
            Assert.AreEqual(5, h.Mastery(second), "6성 관문에서 멈추고 나머지는 경험치로 남는다");
            Assert.AreEqual(6, h.FormGate(second));
        }

        static int Character_Mastery(Character h, MartialArt a)
        {
            h.PassForm(a);
            return h.Mastery(a);
        }

        [TestCase(InnerNature.Yang, InnerNature.Harmony)]
        [TestCase(InnerNature.Harmony, InnerNature.Yang)]
        [TestCase(InnerNature.Harmony, InnerNature.Harmony)]
        public void OnlyYinToYinAndYangToYangCarry(InnerNature from, InnerNature to)
        {
            var h = Pair(from, to, out _, out var second);
            Assert.IsFalse(h.KeepsLegacy(second));
            h.SwitchMind(second);
            Assert.IsFalse(h.LegacyPending);
            h.GainMastery(second, 50);
            Assert.AreEqual(2, h.Mastery(second));
        }

        [Test]
        public void SwitchingAgainBeforeTheFirstStageLosesTheLegacy()
        {
            var h = Pair(InnerNature.Yang, InnerNature.Yang, out var first, out var second);
            h.SwitchMind(second);
            Assert.IsTrue(h.LegacyPending);
            h.SwitchMind(first);
            Assert.IsFalse(h.LegacyPending);
        }

        [Test]
        public void PendingLegacySurvivesSaving()
        {
            var h = Pair(InnerNature.Yin, InnerNature.Yin, out var first, out var second);
            first.name = "첫째";
            second.name = "둘째";
            h.SwitchMind(second);
            var loaded = new GameState(new Character("?", new Stats()));
            string json = SaveGame.Write(new GameState(h), "{}");
            SaveGame.Read(json, loaded, n => n == "첫째" ? first : n == "둘째" ? second : null);
            Assert.IsTrue(loaded.Hero.LegacyPending);
            loaded.Hero.GainMastery(second, 50);
            Assert.AreEqual(5, loaded.Hero.Mastery(second));
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
            Assert.IsTrue(Art("Jeonjinsim").keepsMastery);
            Assert.IsTrue(Art("ManryuSim").keepsMastery);
            Assert.IsFalse(Art("Cheongsimgyeol").keepsMastery);
            Assert.IsFalse(Art("Eumsagong").keepsMastery);
            Assert.IsNotEmpty(Art("Eumsagong").forms);
        }
    }
}
