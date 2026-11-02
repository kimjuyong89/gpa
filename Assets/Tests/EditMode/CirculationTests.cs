using System;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class CirculationTests
    {
        const float Dt = 1 / 60f;

        static Circulation New(Effort e, int realm = 0, int wis = 5, int con = 5, bool injured = false, float fatigue = 0, int seed = 1) =>
            new(e, realm, wis, con, injured, fatigue, new System.Random(seed));

        // 사람처럼 lag초 늦게 반응하는 자동 조종. 막대가 칸 가운데보다 낮으면 누른다.
        static Circulation Play(Circulation c, float lag = 0)
        {
            var seen = new float[Math.Max(1, (int)(lag / Dt))];
            for (int t = 0; t < 60 * 180 && c.Result == CirculationEnd.Ongoing; t++)
            {
                int i = t % seen.Length;
                float old = t < seen.Length ? c.Speed : seen[i];
                seen[i] = c.Speed;
                c.Tick(Dt, old < c.WantNow);
            }
            return c;
        }

        static float Rate(Effort e, float lag, int realm = 0, bool injured = false, float fatigue = 0)
        {
            int wins = Enumerable.Range(0, 200).Count(s => Play(New(e, realm, injured: injured, fatigue: fatigue, seed: s), lag).Result == CirculationEnd.Complete);
            float rate = wins / 200f;
            Debug.Log($"[CIR] {e} 경지{realm} 반응{lag}초{(injured ? " 내상" : "")}{(fatigue > 0 ? $" 피로{fatigue}" : "")} 완주 {rate:P0}");
            return rate;
        }

        [Test]
        public void HoldingTheBreathOverheats()
        {
            var c = New(Effort.Steady);
            for (int i = 0; i < 60 * 120 && c.Result == CirculationEnd.Ongoing; i++) c.Tick(Dt, true);
            Assert.AreNotEqual(CirculationEnd.Dispersed, c.Result);
            Assert.AreNotEqual(CirculationEnd.Ongoing, c.Result);
        }

        [Test]
        public void NotBreathingScattersTheQi()
        {
            var c = New(Effort.Steady);
            for (int i = 0; i < 60 * 120 && c.Result == CirculationEnd.Ongoing; i++) c.Tick(Dt, false);
            Assert.AreEqual(CirculationEnd.Dispersed, c.Result);
        }

        [Test]
        public void GoodBreathingCompletesAtEveryEffort()
        {
            foreach (Effort e in Enum.GetValues(typeof(Effort)))
            {
                var c = Play(New(e));
                Assert.AreEqual(CirculationEnd.Complete, c.Result, e.ToString());
                Assert.Greater(c.Quality, 0.8f, e.ToString());
            }
        }

        [Test]
        public void BlockedPointStopsTheQiUntilPushed()
        {
            var c = New(Effort.Reckless);
            int at = Array.IndexOf(c.Blocked, true);
            Assert.GreaterOrEqual(at, 2);
            while (c.Segment < at && c.Result == CirculationEnd.Ongoing) c.Tick(Dt, c.Speed < c.WantNow);
            for (int i = 0; i < 60 * 3; i++) c.Tick(Dt, false); // 힘을 빼면 못 지나간다
            Assert.Less(c.Travel, at + 1);
        }

        [Test]
        public void HigherRealmHasALongerRoute()
        {
            Assert.AreEqual(8, New(Effort.Steady).Nodes.Length);
            Assert.AreEqual(10, New(Effort.Steady, realm: 1).Nodes.Length);
            Assert.AreEqual(12, New(Effort.Steady, realm: 2).Nodes.Length);
        }

        [Test]
        public void StatsAndConditionChangeTheRun()
        {
            Assert.Greater(New(Effort.Steady, wis: 10).Width, New(Effort.Steady, wis: 0).Width);
            Assert.Greater(New(Effort.Steady, injured: true).Blocked.Count(b => b), 0);
            Assert.AreEqual(0, New(Effort.Steady).Blocked.Count(b => b));
        }

        [Test]
        public void HarderRunsPayMore()
        {
            float steady = Play(New(Effort.Steady)).Reward, reckless = Play(New(Effort.Reckless)).Reward;
            Assert.Greater(steady, 1f, "잘 돌리면 자동 수련보다 낫다");
            Assert.Greater(reckless, steady);
        }

        [Test]
        public void FinishedRunGivesInnerPowerAndFatigue()
        {
            var h = new Character("나", new Stats { Wis = 4 });
            int gain = h.ApplyCirculation(Play(Circulation.For(h, Effort.Earnest, new System.Random(1))));
            Assert.Greater(gain, 12, "자동 수련 12보다 많다");
            Assert.AreEqual(gain, h.InnerPower);
            Assert.AreEqual(20, h.Fatigue);
        }

        [Test]
        public void OverheatingInjuresAndDispersingOnlyTires()
        {
            var h = new Character("나", new Stats());
            var hot = Circulation.For(h, Effort.Reckless, new System.Random(1));
            for (int i = 0; i < 60 * 120 && hot.Result == CirculationEnd.Ongoing; i++) hot.Tick(Dt, true);
            Assume.That(hot.Result, Is.EqualTo(CirculationEnd.Overheated));
            Assert.AreEqual(0, h.ApplyCirculation(hot));
            Assert.IsTrue(h.InnerInjured);

            var g = new Character("나", new Stats());
            var cold = Circulation.For(g, Effort.Steady, new System.Random(1));
            for (int i = 0; i < 60 * 120 && cold.Result == CirculationEnd.Ongoing; i++) cold.Tick(Dt, false);
            Assert.AreEqual(0, g.ApplyCirculation(cold));
            Assert.IsFalse(g.InnerInjured);
            Assert.AreEqual(20, g.Fatigue);
        }

        // 난이도표. 사람 반응(0.2초)으로 안정은 대부분 완주하고, 무리는 위험해야 한다.
        [Test]
        public void DifficultyCurve()
        {
            foreach (float lag in new[] { 0.12f, 0.16f, 0.2f, 0.24f, 0.28f, 0.32f })
            foreach (Effort e in Enum.GetValues(typeof(Effort)))
                Rate(e, lag);
            Rate(Effort.Steady, 0.2f, realm: 2);
            Rate(Effort.Earnest, 0.2f, injured: true);
            Rate(Effort.Earnest, 0.2f, fatigue: 0.9f);

            Assert.That(Rate(Effort.Steady, 0.2f), Is.GreaterThan(0.85f));
            Assert.That(Rate(Effort.Earnest, 0.2f), Is.GreaterThan(0.5f));
            Assert.That(Rate(Effort.Reckless, 0.12f), Is.GreaterThan(0.5f), "무리는 숨을 앞서 읽는 숙련자의 길이다");
            Assert.That(Rate(Effort.Reckless, 0.2f), Is.LessThan(Rate(Effort.Steady, 0.2f)), "무리가 더 어렵다");
        }
    }
}
