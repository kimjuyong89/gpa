using System.IO;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class SectTests
    {
        MartialArt sword, mind, secret;
        SectData cheongun;
        GameState state;

        static MartialArt Art(string name)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.displayName = name;
            return a;
        }

        [SetUp]
        public void SetUp()
        {
            sword = Art("청운쾌검");
            mind = Art("청심결");
            secret = Art("청운낙일검");
            cheongun = ScriptableObject.CreateInstance<SectData>();
            cheongun.id = "청운검문";
            cheongun.alignment = "정파";
            cheongun.master = "윤백산";
            cheongun.discipleAffinity = 10;
            cheongun.secretArt = secret;
            cheongun.arts = new[] { new SectArt { art = sword }, new SectArt { art = mind, contribution = 30 } };

            var heuksa = ScriptableObject.CreateInstance<SectData>();
            heuksa.id = "흑사방";
            heuksa.alignment = "사파";
            heuksa.onCaught = CaughtPenalty.Hunt;

            state = new GameState(new Character("주인공", new Stats()), new[] { cheongun, heuksa });
        }

        [Test]
        public void JoinTeachesFreeArtsOnly()
        {
            Assert.IsTrue(state.Join("청운검문"));
            Assert.AreEqual("청운검문", state.Sect);
            CollectionAssert.Contains(state.Hero.Arts, sword);
            CollectionAssert.DoesNotContain(state.Hero.Arts, mind);
            Assert.AreEqual("소속", state.JoinBlocker("청운검문"));
        }

        [Test]
        public void OppositeAlignmentOrBadReputationBlocksJoin()
        {
            state.Set("사파", 3);
            Assert.AreEqual("성향", state.JoinBlocker("청운검문"));
            state.Set("정파", 1);
            Assert.AreEqual("", state.JoinBlocker("청운검문"));

            state.Set("평판.청운검문", -3);
            Assert.AreEqual("평판", state.JoinBlocker("청운검문"));
            Assert.IsFalse(state.Join("청운검문"));
        }

        [Test]
        public void SectArtNeedsContribution()
        {
            state.Join("청운검문");
            Assert.IsFalse(state.LearnSectArt("청심결"));
            state.Add("공헌.청운검문", 30);
            Assert.IsTrue(state.LearnSectArt("청심결"));
            Assert.IsTrue(state.Knows("청심결"));
        }

        [Test]
        public void DiscipleLiftsMasteryCapAndTeachesSecret()
        {
            state.Join("청운검문");
            state.Hero.GainMastery(sword, 100000);
            state.Hero.PassForm(sword);
            Assert.AreEqual(GameState.NormalMasteryCap, state.Hero.Mastery(sword));

            state.Add("공헌.청운검문", GameState.InnerContribution);
            state.Promote(); // 내문제자
            Assert.IsFalse(state.BecomeDisciple(), "호감 부족");
            state.Set("호감.윤백산", 10);
            Assert.IsTrue(state.BecomeDisciple());

            while (state.Hero.FormGate(sword) > 0) state.Hero.PassForm(sword);
            Assert.AreEqual(10, state.Hero.Mastery(sword));
            CollectionAssert.Contains(state.Hero.Arts, secret);
        }

        [Test]
        public void SecretJobsGetYouExpelled()
        {
            state.Join("청운검문");
            Assert.IsFalse(state.SecretJob(60));
            Assert.IsTrue(state.SecretJob(40));

            Assert.IsNull(state.Sect);
            Assert.AreEqual("파문", state.JoinBlocker("청운검문"));
            Assert.AreEqual(0, state.Get("추격.청운검문"));
        }

        [Test]
        public void CaughtByHuntingSectMeansPursuit()
        {
            state.Join("흑사방");
            state.SecretJob(GameState.CaughtAt);
            Assert.AreEqual(1, state.Get("추격.흑사방"));
        }

        [Test]
        public void SwitchingSectsOpenlyIsBetrayal()
        {
            state.Join("흑사방");
            state.Join("청운검문");
            Assert.AreEqual(1, state.Get("배신.흑사방"));
            Assert.AreEqual(-5, state.Get("평판.흑사방"));
        }

        [Test]
        public void InkJoinsAndExpelsThroughScript()
        {
            var json = DialogueTests.CompileFile("Baekrojin.ink");
            state.Hero.Money = 0;

            var join = new Dialogue(json, state);
            join.Start("청운검문_입문");
            DialogueTests.Drain(join);
            join.Choose(0); // 제자로 받아 주십시오
            DialogueTests.Drain(join);
            join.Choose(0); // 약한 사람을 지키고 싶습니다
            DialogueTests.Drain(join);
            Assert.AreEqual("청운검문", state.Sect);
            Assert.AreEqual(3, state.Get("호감.윤백산"));

            for (int i = 0; i < 3; i++)
            {
                var job = new Dialogue(json, state);
                job.Start("흑사방_밀수_의뢰");
                DialogueTests.Drain(job);
                job.Choose(0); // 몰래 밀수품을 옮긴다 (40씩)
                DialogueTests.Drain(job);
            }
            Assert.IsNull(state.Sect);
            Assert.AreEqual(90, state.Hero.Money);
            CollectionAssert.Contains(state.Expelled, "청운검문");
        }
    }
}
