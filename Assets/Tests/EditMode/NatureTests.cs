using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 내공 속성: 음, 양, 음양조화.
    public class NatureTests
    {
        static MartialArt Outer(Affinity affinity = Affinity.None)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 100;
            a.affinity = affinity;
            a.lineage = Lineage.Orthodox;
            return a;
        }

        static Combatant Unit(InnerNature nature, bool ally = true) =>
            new("가", ally, new Stats { Con = 50 }, new[] { Outer() }) { Nature = nature };

        [TestCase(InnerNature.Yin, InnerNature.Yang, 1.15f)]
        [TestCase(InnerNature.Yang, InnerNature.Yin, 1.15f)]
        [TestCase(InnerNature.Yang, InnerNature.Yang, 1f)]
        [TestCase(InnerNature.Yin, InnerNature.Yin, 1f)]
        [TestCase(InnerNature.None, InnerNature.Yin, 1f)]
        [TestCase(InnerNature.Yin, InnerNature.None, 1f)]
        [TestCase(InnerNature.Harmony, InnerNature.Yin, 1.15f)]
        [TestCase(InnerNature.Harmony, InnerNature.Yang, 1.15f)]
        [TestCase(InnerNature.Harmony, InnerNature.None, 1f)]
        [TestCase(InnerNature.Harmony, InnerNature.Harmony, 1f)]
        [TestCase(InnerNature.Yin, InnerNature.Harmony, 1f)]
        [TestCase(InnerNature.Yang, InnerNature.Harmony, 1f)]
        public void OpposedNaturesHitHarder(InnerNature attacker, InnerNature target, float expected)
        {
            Assert.AreEqual(expected, Unit(attacker).NatureMultiplier(Unit(target)), 0.001f);
        }

        [TestCase(InnerNature.Yang, Affinity.Gang, 1.1f)]
        [TestCase(InnerNature.Yang, Affinity.Yu, 1f)]
        [TestCase(InnerNature.Yin, Affinity.Yu, 1.1f)]
        [TestCase(InnerNature.Yin, Affinity.Gang, 1f)]
        [TestCase(InnerNature.Harmony, Affinity.Gang, 1.1f)]
        [TestCase(InnerNature.Harmony, Affinity.Yu, 1.1f)]
        [TestCase(InnerNature.Yang, Affinity.Kwae, 1f)]
        [TestCase(InnerNature.None, Affinity.Gang, 1f)]
        public void NatureFavorsItsKindOfArt(InnerNature nature, Affinity affinity, float expected)
        {
            Assert.AreEqual(expected, Unit(nature).NatureArtBonus(Outer(affinity)), 0.001f);
        }

        [Test]
        public void BasicArtsGainNothingFromNature()
        {
            var basic = Outer(Affinity.Gang);
            basic.lineage = Lineage.Common;
            Assert.AreEqual(1f, Unit(InnerNature.Yang).NatureArtBonus(basic));
        }

        [Test]
        public void HeroTakesNatureFromTheActiveMind()
        {
            var yang = ScriptableObject.CreateInstance<MartialArt>();
            yang.kind = ArtKind.Mind;
            yang.nature = InnerNature.Yang;
            var yin = ScriptableObject.CreateInstance<MartialArt>();
            yin.kind = ArtKind.Mind;
            yin.nature = InnerNature.Yin;
            var h = new Character("나", new Stats(), new[] { yang, yin });
            Assert.AreEqual(InnerNature.Yang, h.ToCombatant().Nature);
            h.ActiveMind = yin;
            Assert.AreEqual(InnerNature.Yin, h.ToCombatant().Nature);
            Assert.AreEqual(InnerNature.Yin, h.InnerDemon().Nature, "심마도 같은 속성이다");
        }

        [Test]
        public void NatureChangesBattleDamage()
        {
            int Hit(InnerNature attacker, InnerNature target)
            {
                var art = Outer();
                var a = new Combatant("가", true, new Stats { Light = 10 }, new[] { art }) { Nature = attacker };
                var t = new Combatant("나", false, new Stats { Con = 50 }, new[] { art }) { Nature = target };
                var b = new Battle(new[] { a }, new[] { t }, new System.Random(1)) { DamageSpread = 0 };
                b.BeginTurn();
                int hp = t.Hp;
                b.UseArt(art, t);
                return hp - t.Hp;
            }
            Assert.Greater(Hit(InnerNature.Yin, InnerNature.Yang), Hit(InnerNature.Yin, InnerNature.Yin));
        }

        [Test]
        public void SetupGivesEnemiesTheirNature()
        {
            Assert.AreEqual(InnerNature.Yin, new UnitSetup { name = "적", nature = InnerNature.Yin, arts = new[] { Outer() }, stats = new Stats() }.Create().Nature);
        }

        [Test]
        public void DataMatchesTheSetting()
        {
            MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"Assets/_Project/Data/MartialArts/{file}.asset");
            Assert.AreEqual(InnerNature.Yang, Art("Jeonjinsim").nature);
            Assert.AreEqual(InnerNature.Yang, Art("Cheongsimgyeol").nature);
            Assert.AreEqual(InnerNature.Yin, Art("Eumsagong").nature);
            Assert.AreEqual(InnerNature.Harmony, Art("ManryuSim").nature);
            var thugs = AssetDatabase.LoadAssetAtPath<BattleData>("Assets/_Project/Data/Battles/HeuksaThugs.asset");
            Assert.AreEqual(InnerNature.Yin, thugs.enemies[0].nature);
        }
    }
}
