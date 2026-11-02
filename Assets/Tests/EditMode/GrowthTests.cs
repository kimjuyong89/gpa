using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class GrowthTests
    {
        static MartialArt Art(int cost = 10, int power = 10)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.displayName = "검법";
            a.kind = ArtKind.Outer;
            a.innerCost = cost;
            a.power = power;
            return a;
        }

        static Character Hero(params MartialArt[] arts) => new("주인공", new Stats(), arts);

        [Test]
        public void TrainingStopsAtFatigueLimit()
        {
            var c = Hero(); // 피로 최대 100, 토납 20씩
            int count = 0;
            while (c.Train(Training.Tonap)) count++;

            Assert.AreEqual(5, count);
            Assert.IsTrue(c.Rest(atSect: true));
            Assert.AreEqual(0, c.Fatigue);
        }

        [Test]
        public void InnRestCostsMoney()
        {
            var c = Hero();
            c.Money = Character.InnCost - 1;
            Assert.IsFalse(c.Rest(atSect: false));

            c.Money = Character.InnCost;
            Assert.IsTrue(c.Rest(atSect: false));
            Assert.AreEqual(0, c.Money);
        }

        [Test]
        public void TiredTrainingIsHalfAsEffective()
        {
            var c = Hero();
            c.Train(Training.Tonap);
            int fresh = c.InnerPower;

            c.Fatigue = 80; // 80% 이상
            c.Train(Training.Tonap);
            Assert.AreEqual(fresh / 2, c.InnerPower - fresh);
        }

        [Test]
        public void LaborRaisesConEveryFiveSessions()
        {
            var c = Hero();
            for (int i = 0; i < 5; i++)
            {
                c.Train(Training.Labor);
                c.Rest(atSect: true);
            }
            Assert.AreEqual(1, c.Stats.Con);
            Assert.AreEqual(50, c.Money);
        }

        [Test]
        public void MasteryLevelsFollowThresholds()
        {
            var sword = Art();
            var c = Hero(sword);
            Assert.AreEqual(1, c.Mastery(sword));
            c.GainMastery(sword, 50);
            Assert.AreEqual(2, c.Mastery(sword));
            c.GainMastery(sword, 100);
            Assert.AreEqual(3, c.Mastery(sword));
            c.GainMastery(sword, 100000);
            while (c.FormGate(sword) > 0) c.PassForm(sword);
            Assert.AreEqual(10, c.Mastery(sword));
        }

        [Test]
        public void BreakthroughNeedsFullInnerPowerAndEnlightenmentAtWalls()
        {
            var c = Hero();
            Assert.IsFalse(c.CanAttemptBreakthrough);

            c.InnerPower = c.InnerPowerToBreak;
            c.FinishBreakthrough(true);
            Assert.AreEqual(Realm.Iryu, c.Realm);
            Assert.AreEqual(0, c.InnerPower);

            c.Realm = Realm.Ilryu;
            c.InnerPower = c.InnerPowerToBreak;
            Assert.IsFalse(c.CanAttemptBreakthrough, "일류→절정은 깨달음이 필요");
            c.Enlightened = true;
            Assert.IsTrue(c.CanAttemptBreakthrough);
        }

        [Test]
        public void FailedBreakthroughCausesInnerInjury()
        {
            var c = Hero();
            c.InnerPower = c.InnerPowerToBreak;

            c.FinishBreakthrough(false);

            Assert.IsTrue(c.InnerInjured);
            Assert.AreEqual(Realm.Samryu, c.Realm);
            Assert.AreEqual(80, c.InnerPower);
        }

        [Test]
        public void MasteryAndRealmCarryIntoBattle()
        {
            var sword = Art(cost: 10);
            var c = Hero(sword);
            c.GainMastery(sword, 150); // 3성
            c.Realm = Realm.Iryu;

            var unit = c.ToCombatant();

            Assert.AreEqual(8, unit.CostOf(sword));
            Assert.AreEqual(50 + 30, unit.MaxHp);
        }

        [Test]
        public void BattleUseRaisesMastery()
        {
            var sword = Art(cost: 0, power: 1);
            var hero = Hero(sword);
            hero.Stats.Light = 10;
            var dummy = new Combatant("허수아비", false, new Stats { Con = 100 }, new MartialArt[0]);
            var unit = hero.ToCombatant();
            var b = new Battle(new[] { unit }, new[] { dummy }, new System.Random(1));

            for (int i = 0; i < 5; i++)
            {
                b.BeginTurn();
                b.UseArt(sword, dummy);
            }
            b.Finish();
            hero.ApplyBattle(unit);

            Assert.AreEqual(2, hero.Mastery(sword)); // 5회 × 10 = 50
        }
    }
}
