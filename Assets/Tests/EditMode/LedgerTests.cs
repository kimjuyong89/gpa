using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;

namespace GPA.Tests
{
    // 장부 사건: 창고에서 장부를 얻고, 네 갈래 중 하나로 끝낸다.
    public class LedgerTests
    {
        const string Q = "대사형의 장부";
        string json;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            json = DialogueTests.CompileFile("Baekrojin.ink");
            state = new GameState(new Character("나", new Stats())) { Hero = { Money = 40 } };
            state.SetQuest(Q, 2, "창고 단서");
        }

        Dialogue Run(string knot, params int[] choices)
        {
            var d = new Dialogue(json, state);
            d.Start(knot);
            DialogueTests.Drain(d);
            foreach (int c in choices)
            {
                d.Choose(c);
                DialogueTests.Drain(d);
            }
            return d;
        }

        void GetLedgerByBribe() => Run("나루터_창고", 1); // 소육 없음 → [쓰러뜨린다, 뇌물, 물러난다]

        [Test]
        public void WarehouseIsClosedWithoutTheHint()
        {
            state.FindQuest(Q).Stage = 1;
            Assert.IsTrue(Run("나루터_창고").Ended);
            Assert.AreEqual(0, state.Get("장부"));
        }

        [Test]
        public void BribeGetsTheLedgerAndClue()
        {
            GetLedgerByBribe();
            Assert.AreEqual(1, state.Get("장부"));
            Assert.AreEqual(1, state.Get("단서.무극진경_4"));
            Assert.AreEqual(3, state.FindQuest(Q).Stage);
            Assert.AreEqual(10, state.Hero.Money);
        }

        [Test]
        public void SoyukDistractsTheGuard()
        {
            state.Set("동료.소육", 1);
            Run("나루터_창고", 0);
            Assert.AreEqual(1, state.Get("장부"));
        }

        [Test]
        public void ReportToMaster()
        {
            GetLedgerByBribe();
            Run("윤백산", 0);
            Assert.AreEqual(1, state.Get("대사형_파문"));
            Assert.AreEqual(2, state.Get("정파"));
            Assert.AreEqual(-5, state.Get("평판.흑사방"));
            AssertClosed();
        }

        [Test]
        public void HeuksaMemberWhoReportsIsHunted()
        {
            var heuksa = UnityEngine.ScriptableObject.CreateInstance<SectData>();
            heuksa.id = "흑사방";
            heuksa.alignment = "사파";
            state.SectDb[heuksa.id] = heuksa;
            state.Sect = "흑사방";

            GetLedgerByBribe();
            Run("윤백산", 0);

            Assert.IsNull(state.Sect);
            Assert.AreEqual(1, state.Get("추격.흑사방"));
        }

        [Test]
        public void CoverUpForGoJinak()
        {
            GetLedgerByBribe();
            Run("고진악", 0);
            Assert.AreEqual(1, state.Get("빚.고진악"));
            AssertClosed();
        }

        [Test]
        public void BlackmailGoJinak()
        {
            GetLedgerByBribe();
            Run("고진악", 1);
            Assert.AreEqual(60, state.Hero.Money);
            Assert.AreEqual(2, state.Get("사파"));
            AssertClosed();
        }

        [Test]
        public void SellToHeuksa()
        {
            GetLedgerByBribe();
            Run("탁웅", 0);
            Assert.AreEqual(1, state.Get("청운검문_몰락"));
            Assert.AreEqual(5, state.Get("평판.흑사방"));
            AssertClosed();
        }

        [Test]
        public void WithoutLedgerMasterAndTakwoongBehaveAsBefore()
        {
            var d = Run("윤백산");
            Assert.AreEqual(new[] { "제자로 받아 주십시오.", "둘러보러 왔을 뿐입니다." }, d.Choices);
        }

        void AssertClosed()
        {
            Assert.AreEqual(2, state.Get("장부"));
            Assert.IsTrue(state.FindQuest(Q).Done);
        }
    }
}
