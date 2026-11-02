using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class DemonTests
    {
        static MartialArt Art()
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 10;
            return a;
        }

        static Character Hero(int wis = 5) =>
            new("나", new Stats { Outer = 10, Light = 10, Con = 10, Wis = wis }, new[] { Art() });

        [Test]
        public void DemonMirrorsHeroArtsRealmAndGear()
        {
            var h = Hero();
            h.Realm = Realm.Iryu;
            var ring = ScriptableObject.CreateInstance<ItemData>();
            ring.kind = ItemKind.Trinket;
            ring.stats.Outer = 2;
            ring.attack = 5;
            h.Bag.Add(ring);
            h.Equip(ring);

            var d = h.InnerDemon();
            Assert.IsFalse(d.IsAlly);
            CollectionAssert.AreEqual(h.Arts, d.Arts);
            Assert.AreEqual(1, d.Realm);
            Assert.AreEqual(12, d.Stats.Outer, "세기 100%, 장비 스탯까지 따라 한다");
            Assert.AreEqual(1, d.Gear.Count);
            Assert.AreEqual("심마1", d.Figure);
        }

        [Test]
        public void WisdomAndSectWeakenAndInjuryStrengthensTheDemon()
        {
            float plain = Hero(wis: 5).DemonStrength();
            Assert.AreEqual(1f, plain, 0.001f);
            Assert.Less(Hero(wis: 10).DemonStrength(), plain);
            Assert.Less(Hero(wis: 5).DemonStrength(0.1f), plain);
            var hurt = Hero();
            hurt.InnerInjured = true;
            Assert.Greater(hurt.DemonStrength(), plain);
        }

        static Battle Fight(Character h, out Combatant hero)
        {
            hero = h.ToCombatant();
            return new Battle(new[] { hero }, new[] { h.InnerDemon() }, new System.Random(1)) { Tempting = true };
        }

        [Test]
        public void SuccumbingHealsButBringsQiDeviationOnce()
        {
            var b = Fight(Hero(), out var hero);
            b.BeginTurn();
            Assert.IsTrue(b.CanSuccumb);
            hero.Hp = 10;
            b.Succumb();
            Assert.AreEqual(10 + hero.MaxHp * 4 / 10, hero.Hp);
            Assert.IsTrue(hero.Has(StatusKind.QiDeviation));
            Assert.IsNull(b.Current, "턴을 쓴다");
            Assert.IsFalse(b.Tempting);
            Assert.IsFalse(b.CanSuccumb, "한 번뿐이다");
        }

        [Test]
        public void SuccumbingWorksEvenWithResistance()
        {
            var mind = ScriptableObject.CreateInstance<MartialArt>();
            mind.kind = ArtKind.Mind;
            mind.resists = new[] { StatusKind.QiDeviation };
            var h = Hero();
            h.Arts.Add(mind);
            var b = Fight(h, out var hero);
            b.BeginTurn();
            b.Succumb();
            Assert.IsTrue(hero.Has(StatusKind.QiDeviation));
        }

        [Test]
        public void WinningWhileTemptedAscendsWithAnInjury()
        {
            var h = Hero();
            h.InnerPower = h.InnerPowerToBreak;
            var b = Fight(h, out var hero);
            b.BeginTurn();
            b.Succumb();
            b.Finish();
            h.ApplyBattle(hero);
            h.FinishBreakthrough(true);
            Assert.AreEqual(Realm.Iryu, h.Realm);
            Assert.IsTrue(h.InnerInjured);
            Assert.AreEqual(0, h.InnerPower);
        }

        [Test]
        public void QiDeviationMakesASoloFighterMiss()
        {
            var a = Art();
            var hero = new Combatant("가", true, new Stats { Light = 10, Con = 500 }, new[] { a });
            var dummy = new Combatant("나", false, new Stats { Light = 10, Con = 500 }, new[] { a });
            var b = new Battle(new[] { hero }, new[] { dummy }, new System.Random(1)) { Tempting = true };
            b.BeginTurn();
            b.Succumb();
            b.BeginTurn();
            for (int i = 0; i < 40 && b.Current != null; i++)
            {
                b.AutoAct();
                b.BeginTurn();
            }
            Assert.IsTrue(b.Events.Any(e => e.Text.EndsWith("기운이 엇나간다")));
        }

        [Test]
        public void DemonKeepsTheHerosSpeed()
        {
            var hurt = Hero();
            hurt.InnerInjured = true;
            Assert.AreEqual(hurt.Stats.Light, hurt.InnerDemon().Stats.Light);
        }

        [Test]
        public void OrdinaryBattlesHaveNoTemptation()
        {
            var a = Art();
            var hero = new Combatant("가", true, new Stats { Light = 10 }, new[] { a });
            var b = new Battle(new[] { hero }, new[] { new Combatant("나", false, new Stats(), new[] { a }) });
            b.BeginTurn();
            Assert.IsFalse(b.CanSuccumb);
        }
    }
}
