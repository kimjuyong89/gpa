using System.Collections.Generic;
using System.IO;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using Ink;
using Stats = GPA.Combat.Stats;
using NUnit.Framework;

namespace GPA.Tests
{
    public class DialogueTests
    {
        const string DialogueDir = "Assets/_Project/Dialogue";

        // INCLUDE를 Dialogue 폴더 기준으로 찾는다.
        class Files : IFileHandler
        {
            public string ResolveInkFilename(string includeName) => Path.Combine(DialogueDir, includeName);
            public string LoadInkFileContents(string fullFilename) => File.ReadAllText(fullFilename);
        }

        public static string Compile(string source)
        {
            var errors = new List<string>();
            var story = new Compiler(source, new Compiler.Options
            {
                fileHandler = new Files(),
                errorHandler = (message, type) => { if (type == ErrorType.Error) errors.Add(message); },
            }).Compile();
            Assert.IsEmpty(errors, string.Join("\n", errors));
            return story.ToJson();
        }

        public static string CompileFile(string name) => Compile(File.ReadAllText(Path.Combine(DialogueDir, name)));

        static GameState NewState(int money) => new(new Character("주인공", new Stats())) { Hero = { Money = money } };

        // 선택지나 끝이 나올 때까지 읽은 줄
        public static List<string> Drain(Dialogue d)
        {
            var lines = new List<string>();
            while (d.CanContinue) lines.Add(d.Continue());
            return lines;
        }

        const string Shop = @"
EXTERNAL add(key, amount)
EXTERNAL money()
EXTERNAL pay(amount)
=== shop ===
+ {money() >= 20} [buy]
    ~ pay(20)
    ~ add(""호감.상인"", 1)
    bought
    -> END
+ [leave]
    -> END
";

        [Test]
        public void ChoicesAndEffectsUseGameState()
        {
            var state = NewState(30);
            var d = new Dialogue(Compile(Shop), state);
            d.Start("shop");
            Drain(d);
            Assert.AreEqual(new[] { "buy", "leave" }, d.Choices);

            d.Choose(0);
            Assert.Contains("bought", Drain(d));
            Assert.AreEqual(10, state.Hero.Money);
            Assert.AreEqual(1, state.Get("호감.상인"));
            Assert.IsTrue(d.Ended);

            var poor = new Dialogue(Compile(Shop), state);
            poor.Start("shop");
            Drain(poor);
            Assert.AreEqual(new[] { "leave" }, poor.Choices);
        }

        [Test]
        public void TagsAreExposed()
        {
            var d = new Dialogue(Compile("Hey! # battle: thugs\n-> END"), NewState(0));
            d.Continue();
            Assert.Contains("battle: thugs", d.Tags);
        }

        [Test]
        public void SplitSpeaker()
        {
            Assert.AreEqual(("점소이", "어서 옵쇼"), Dialogue.Split("점소이: 어서 옵쇼"));
            Assert.AreEqual(("", "바람이 분다."), Dialogue.Split("바람이 분다."));
        }

        [Test]
        public void BaekrojinRumorStartsLedgerQuest()
        {
            var json = CompileFile("Baekrojin.ink");
            var state = NewState(30);

            var inn = new Dialogue(json, state);
            inn.Start("객잔_점소이");
            Drain(inn);
            Assert.AreEqual(3, inn.Choices.Count);
            inn.Choose(1); // 요즘 마을에 별일 없소?
            Drain(inn);
            inn.Choose(0);
            Drain(inn);

            Assert.AreEqual(1, state.FindQuest("대사형의 장부").Stage);
            Assert.AreEqual(1, state.Get("호감.점소이"));

            var dock = new Dialogue(json, state);
            dock.Start("나루터_흑사방");
            Drain(dock);
            dock.Choose(1); // 엽전 10냥
            Drain(dock);

            Assert.AreEqual(2, state.FindQuest("대사형의 장부").Stage);
            Assert.AreEqual(20, state.Hero.Money);
            Assert.AreEqual(1, state.Get("사파"));
        }

        [Test]
        public void SoyukJoinsAfterMeal()
        {
            var state = NewState(30);
            var d = new Dialogue(CompileFile("Baekrojin.ink"), state);
            d.Start("뒷골목_소육");
            Drain(d);
            d.Choose(0); // 소매치기를 붙잡는다
            Drain(d);
            d.Choose(0); // 국밥 (5냥)
            Drain(d);

            Assert.AreEqual(1, state.Get("동료.소육"));
            Assert.AreEqual(25, state.Hero.Money);
        }

        // 화면은 전투 태그에서 멈추고 전투 결과를 기록한 뒤 이어 간다. 대본이 그 결과로 갈라지는지 본다.
        [Test]
        public void DockFightBranchesOnBattleResult()
        {
            var state = NewState(30);
            var d = new Dialogue(CompileFile("Baekrojin.ink"), state);
            d.Start("나루터_흑사방");
            Drain(d);
            d.Choose(0); // 물러서지 않는다
            while (d.CanContinue)
            {
                d.Continue();
                if (d.Tags.Contains("battle: 흑사방_졸개들")) break;
            }

            state.Set("전투.흑사방_졸개들", -1);
            Drain(d);

            Assert.AreEqual(0, state.Hero.Money, "지면 전낭을 털린다");
            Assert.AreEqual(1, state.Get("정파"));
        }
    }
}
