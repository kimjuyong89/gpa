using System;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class FormPracticeTests
    {
        const float Dt = 1 / 120f;

        static MartialArt Art(Affinity affinity = Affinity.Gang, string name = "시험검")
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.displayName = name;
            a.affinity = affinity;
            a.forms = new[] { "일초", "이초", "삼초" };
            return a;
        }

        static float Gauss(System.Random r) =>
            (float)(Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble()));

       
        static FormPractice Play(FormPractice f, float sigma, System.Random rng)
        {
            var plan = f.Notes.Select(n => (at: n.Time + Gauss(rng) * sigma, n.Lane)).OrderBy(p => p.at).ToList();
            int next = 0;
            while (!f.Done)
            {
                f.Tick(Dt);
                while (next < plan.Count && plan[next].at <= f.Clock) f.Press(plan[next++].Lane);
            }
            return f;
        }

        static float PassRate(int gate, float sigma, Affinity affinity = Affinity.Gang, int wis = 5, float fatigue = 0)
        {
            var rng = new System.Random(1);
            int wins = Enumerable.Range(0, 200).Count(_ => Play(new FormPractice(Art(affinity), gate, wis, fatigue), sigma, rng).Passed);
            float rate = wins / 200f;
            Debug.Log($"[FORM] {gate}성 {affinity} 흔들림{sigma}초 피로{fatigue} 통과 {rate:P0}");
            return rate;
        }

        [Test]
        public void PerfectPlayPassesAndDoingNothingFails()
        {
            var good = Play(new FormPractice(Art(), 10, 5, 0), 0, new System.Random(1));
            Assert.IsTrue(good.Passed);
            Assert.AreEqual(1f, good.Score, 0.001f);

            var idle = new FormPractice(Art(), 6, 5, 0);
            while (!idle.Done) idle.Tick(Dt);
            Assert.IsFalse(idle.Passed);
            Assert.AreEqual(idle.Notes.Count, idle.Misses);
        }

        [Test]
        public void WrongLaneAndEarlyPressHitNothing()
        {
            var f = new FormPractice(Art(), 6, 5, 0);
            var first = f.Notes[0];
            Assert.AreEqual(Hit.None, f.Press(first.Lane), "아직 멀다");
            f.Tick(first.Time);
            Assert.AreEqual(Hit.None, f.Press((first.Lane + 1) % FormPractice.Lanes));
            Assert.AreEqual(Hit.Perfect, f.Press(first.Lane));
        }

        [Test]
        public void PatternIsTheSameEveryTimeSoItCanBeLearned()
        {
            var a = new FormPractice(Art(), 9, 5, 0);
            var b = new FormPractice(Art(), 9, 5, 0);
            CollectionAssert.AreEqual(a.Notes.Select(n => (n.Time, n.Lane)), b.Notes.Select(n => (n.Time, n.Lane)));
            var other = new FormPractice(Art(name: "다른검"), 9, 5, 0);
            CollectionAssert.AreNotEqual(a.Notes.Select(n => n.Lane), other.Notes.Select(n => n.Lane));
        }

        [Test]
        public void AffinityChangesTheRhythm()
        {
            float Span(Affinity x) => new FormPractice(Art(x), 6, 5, 0).Notes[^1].Time;
            Assert.Greater(Span(Affinity.Gang), Span(Affinity.Yu));
            Assert.Greater(Span(Affinity.Yu), Span(Affinity.Kwae));
        }

        [Test]
        public void HigherGatesAreLongerFasterAndStricter()
        {
            FormPractice six = new(Art(), 6, 5, 0), ten = new(Art(), 10, 5, 0);
            Assert.Greater(ten.Notes.Count, six.Notes.Count);
            Assert.Greater(ten.Need, six.Need);
            Assert.Less(ten.Notes[1].Time - ten.Notes[0].Time, six.Notes[1].Time - six.Notes[0].Time);
        }

        [Test]
        public void FormNamesFollowTheNotes()
        {
            var f = new FormPractice(Art(), 6, 5, 0);
            Assert.AreEqual("일초", f.CurrentForm);
            foreach (var n in f.Notes.Where(n => n.Form == 0)) n.Hit = Hit.Miss;
            Assert.AreEqual("이초", f.CurrentForm);
            Assert.AreEqual(2, f.Notes[^1].Form);
        }

        [Test]
        public void MasteryStopsAtGatesUntilTheFormIsPassed()
        {
            var art = Art();
            var h = new Character("나", new Stats(), new[] { art });
            h.GainMastery(art, 100000);
            Assert.AreEqual(5, h.Mastery(art), "경험치가 차도 6성은 연마해야 한다");
            Assert.AreEqual(6, h.FormGate(art));

            h.PassForm(art);
            Assert.AreEqual(8, h.Mastery(art));
            Assert.AreEqual(9, h.FormGate(art));
            h.PassForm(art);
            Assert.AreEqual(9, h.Mastery(art));
            Assert.AreEqual(10, h.FormGate(art));
            h.PassForm(art);
            Assert.AreEqual(10, h.Mastery(art));
            Assert.AreEqual(0, h.FormGate(art));
        }

        [Test]
        public void GateOpensOnlyWhenExpIsEnoughAndCapAllows()
        {
            var art = Art();
            var h = new Character("나", new Stats(), new[] { art });
            Assert.AreEqual(0, h.FormGate(art), "경험치가 모자라다");

            h.GainMastery(art, 100000);
            h.PassForm(art);
            h.MasteryCaps[art] = 7;
            Assert.AreEqual(7, h.Mastery(art));
            Assert.AreEqual(0, h.FormGate(art), "일반 제자는 9성 관문까지 가지 못한다");
        }

        [Test]
        public void FailingCostsFatigueOnly()
        {
            var art = Art();
            var h = new Character("나", new Stats(), new[] { art });
            h.GainMastery(art, 100000);
            var idle = new FormPractice(art, h.FormGate(art), 0, 0);
            while (!idle.Done) idle.Tick(Dt);
            h.ApplyForm(idle);
            Assert.AreEqual(25, h.Fatigue);
            Assert.AreEqual(5, h.Mastery(art));

            h.ApplyForm(Play(new FormPractice(art, h.FormGate(art), 0, 0), 0, new System.Random(1)));
            Assert.AreEqual(50, h.Fatigue);
            Assert.AreEqual(8, h.Mastery(art));
        }

        // 만류귀종으로 가는 관문은 거의 흔들리지 않아야 넘는다.
        [Test]
        public void FinalGateNeedsNearPerfectTiming()
        {
            var art = Art();
            art.ascendsTo = Art();
            float Rate(float sigma)
            {
                var rng = new System.Random(1);
                float rate = Enumerable.Range(0, 200).Count(_ => Play(new FormPractice(art, 10, 5, 0), sigma, rng).Passed) / 200f;
                Debug.Log($"[FORM] 만류귀종 관문 흔들림{sigma}초 통과 {rate:P0}");
                return rate;
            }
            foreach (float sigma in new[] { 0.03f, 0.05f, 0.07f, 0.09f }) Rate(sigma);
            Assert.That(Rate(0.03f), Is.GreaterThan(0.9f), "연습하면 넘을 수 있다");
            Assert.That(Rate(0.09f), Is.LessThan(0.1f), "대충은 못 넘는다");
        }

        // 난이도표. 타이밍 흔들림 0.06초(잘 치는 사람)면 6성을 넘고, 관문이 높을수록 정확해야 한다.
        [Test]
        public void DifficultyCurve()
        {
            foreach (float sigma in new[] { 0.04f, 0.06f, 0.08f, 0.1f, 0.13f })
            foreach (int gate in new[] { 6, 9, 10 })
                PassRate(gate, sigma);
            PassRate(10, 0.08f, Affinity.Kwae);
            PassRate(6, 0.08f, fatigue: 0.9f);

            Assert.That(PassRate(6, 0.06f), Is.GreaterThan(0.9f));
            Assert.That(PassRate(10, 0.04f), Is.GreaterThan(0.9f));
            Assert.That(PassRate(10, 0.1f), Is.LessThan(PassRate(6, 0.1f)));
        }
    }
}
