using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class MountainTests
    {
        static GameState Life()
        {
            var s = new GameState(new Character("나", new Stats { Con = 8 }));
            s.EnterMountain();
            return s;
        }

        [Test]
        public void EnteringSetsUpTheFirstDay()
        {
            var s = Life();
            Assert.IsTrue(s.InMountain);
            Assert.AreEqual(GameState.MountainNode, s.Location);
            Assert.AreEqual(1, s.MountainDay);
            Assert.Greater(s.Hunger, 0);
            Assert.Greater(s.Food, 0);
        }

        [Test]
        public void EatingAndFireSpendSupplies()
        {
            var s = Life();
            int hunger = s.Hunger, warmth = s.Warmth;
            Assert.IsTrue(s.Eat());
            Assert.AreEqual(hunger + 30, s.Hunger);
            Assert.AreEqual(1, s.Food);
            Assert.IsTrue(s.MakeFire());
            Assert.AreEqual(warmth + 30, s.Warmth);
            Assert.AreEqual(1, s.Firewood);

            s.Eat();
            Assert.IsFalse(s.Eat(), "먹을 것이 없다");
            Assert.AreEqual(100, s.Hunger, "100을 넘지 않는다");
        }

        [Test]
        public void SleepAdvancesTheDayAndRestsTheBody()
        {
            var s = Life();
            s.Hero.Fatigue = 50;
            s.Hero.InnerInjured = true;
            s.SleepInMountain();
            Assert.AreEqual(2, s.MountainDay);
            Assert.AreEqual(40, s.Hunger);
            Assert.AreEqual(40, s.Warmth);
            Assert.AreEqual(0, s.Hero.Fatigue);
            Assert.IsFalse(s.Hero.InnerInjured, "배가 차 있으면 낫는다");
        }

        [Test]
        public void HungryBodiesDoNotHeal()
        {
            var s = Life();
            s.Set("산중.허기", 40);
            s.Hero.InnerInjured = true;
            s.SleepInMountain();
            Assert.IsTrue(s.Hero.InnerInjured);
        }

        [Test]
        public void ShelterAndClothesKeepTheColdOut()
        {
            var bare = Life();
            bare.SleepInMountain();
            var built = Life();
            built.Set("산중.움막", 2);
            built.Set("산중.옷", 2);
            built.SleepInMountain();
            Assert.Greater(built.Warmth, bare.Warmth);
            Assert.AreEqual(66, built.Warmth, "최소 추위는 남는다");
        }

        [Test]
        public void WorkTiresAndHungers()
        {
            var s = Life();
            Assert.IsTrue(s.Spend(15));
            Assert.AreEqual(15, s.Hero.Fatigue);
            Assert.AreEqual(67, s.Hunger);
            s.Hero.Fatigue = s.Hero.MaxFatigue - 5;
            Assert.IsFalse(s.Spend(15), "지쳐서 못 한다");
        }

        [Test]
        public void HungerOrColdAtZeroEndsTheRoute()
        {
            var s = Life();
            Assert.IsFalse(s.Collapsed);
            s.Set("산중.허기", 0);
            Assert.IsTrue(s.Collapsed);
            s.Set("산중.허기", 50);
            s.Set("산중.체온", -1);
            Assert.IsTrue(s.Collapsed);
            s.LeaveMountain("백로진");
            Assert.IsFalse(s.Collapsed, "내려오면 의식주는 끝이다");
            Assert.IsFalse(s.InMountain);
        }

        [Test]
        public void GoalIsReachingTheNextRealm()
        {
            var s = Life();
            Assert.IsFalse(s.CanDescend);
            s.Hero.Realm = Realm.Iryu;
            Assert.IsTrue(s.CanDescend);
        }

        [TestCase(7, 10, true)]
        [TestCase(10, 12, true)]
        [TestCase(6, 10, false)]
        [TestCase(7, 9, false)]
        public void FallNeedsLuckAndSpeed(int luck, int light, bool can)
        {
            var s = new GameState(new Character("나", new Stats { Luck = luck, Light = light }));
            Assert.AreEqual(can, s.CanFallIntoMountain);
        }

        [Test]
        public void FallHappensOnce()
        {
            var s = new GameState(new Character("나", new Stats { Luck = 7, Light = 10 }));
            Assert.IsTrue(s.MarkFirstBattle());
            Assert.IsFalse(s.MarkFirstBattle(), "처음만");
            s.EnterMountain();
            Assert.IsFalse(s.CanFallIntoMountain);
            s.LeaveMountain("백로진");
            Assert.IsFalse(s.CanFallIntoMountain, "내려온 뒤에도 다시 떨어지지 않는다");
        }

        [Test]
        public void AlwaysFleeIgnoresTheOdds()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var a = ScriptableObject.CreateInstance<MartialArt>();
                a.kind = ArtKind.Outer;
                var hero = new Combatant("가", true, new Stats { Light = 1 }, new[] { a });
                var foe = new Combatant("나", false, new Stats { Light = 1 }, new[] { a });
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(seed)) { AlwaysFlee = true };
                b.BeginTurn();
                b.Flee();
                Assert.AreEqual(Outcome.Fled, b.Outcome);
            }
        }

        [Test]
        public void GoalMessageFollowsTheRealm()
        {
            var s = Life();
            Assert.AreEqual("", s.MountainGoal);
            s.Hero.Realm = Realm.Iryu;
            StringAssert.Contains("일류까지", s.MountainGoal);
            s.Hero.Realm = Realm.Ilryu;
            StringAssert.Contains("더 오를 곳은 없다", s.MountainGoal);
            s.LeaveMountain("백로진");
            Assert.AreEqual("", s.MountainGoal);
        }

        // 그럴듯한 하루 살이: 나물 3곳과 나무 2곳을 돌고, 먹고 불 피우고, 남은 기운으로 수련하고 잔다.
        // 움막과 옷은 짓지 않는다. 사냥도 없이 맨몸으로 일류까지 닿아야 한다.
        static int DaysToIlryu(int seed, int wis)
        {
            var rng = new System.Random(seed);
            var s = new GameState(new Character("나", new Stats { Con = 6, Wis = wis }));
            s.EnterMountain();
            while (s.Hero.Realm < Realm.Ilryu && !s.Collapsed && s.MountainDay < 200)
            {
                for (int i = 0; i < 3 && s.Spend(10); i++) s.Add("산중.식량", rng.Next(1, 3));
                for (int i = 0; i < 2 && s.Spend(15); i++) s.Add("산중.장작", rng.Next(1, 4));
                while (s.Hero.CanTrain(Training.Tonap) && s.Hunger > 30)
                {
                    s.Hero.Train(Training.Tonap);
                    s.TrainingStrain();
                    if (s.Hero.CanAttemptBreakthrough) s.Hero.FinishBreakthrough(true);
                    while (s.Hunger < 70 && s.Eat()) { }
                }
                while (s.Hunger < 70 && s.Eat()) { }
                while (s.Warmth < 70 && s.MakeFire()) { }
                s.SleepInMountain();
            }
            return s.Hero.Realm >= Realm.Ilryu && !s.Collapsed ? s.MountainDay : -1;
        }

        [Test]
        public void IlryuIsReachableByLivingOffTheMountain()
        {
            var days = System.Linq.Enumerable.Range(0, 100).Select(i => DaysToIlryu(i, 5)).ToList();
            Debug.Log($"[MT] 일류까지 {days.Min()}~{days.Max()}일, 평균 {days.Average():F1}일");
            CollectionAssert.DoesNotContain(days, -1, "살림을 꾸리면 쓰러지지 않고 닿는다");
            Assert.Greater(days.Min(), 5, "며칠 만에 끝나면 산중 수련이 아니다");
            Assert.Less(days.Max(), 60);
        }

        [Test]
        public void TrainingAtASiteRaisesThatStat()
        {
            var h = new Character("나", new Stats { Con = 3, Inner = 3 });
            var run = new Circulation(Effort.Steady, 0, 5, 5, false, 0, new System.Random(1));
            for (int t = 0; t < 60 * 60 && run.Result == CirculationEnd.Ongoing; t++) run.Tick(1 / 60f, run.Speed < run.WantNow);
            for (int i = 0; i < 4; i++) h.ApplyCirculation(run, StatKind.Con);
            Assert.Greater(h.Stats.Con, 3);
            Assert.AreEqual(3, h.Stats.Inner, "내공 스탯 대신 그 터의 스탯이 오른다");
        }

        [Test]
        public void SecretArtIsFoundInResources()
        {
            var art = Resources.Load<MartialArt>("Arts/Cheonsanbo");
            Assert.IsNotNull(art);
            Assert.AreEqual(ArtKind.Movement, art.kind);
            Assert.Greater(art.guard, 0);
        }
    }
}
