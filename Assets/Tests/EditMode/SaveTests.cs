using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class SaveTests
    {
        static MartialArt Art(string assetName)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.name = assetName;
            a.displayName = assetName;
            return a;
        }

        [Test]
        public void RoundTripRestoresHeroAndWorld()
        {
            var sword = Art("Sword");
            var mind = Art("Mind");
            var arts = new Dictionary<string, MartialArt> { ["Sword"] = sword, ["Mind"] = mind };

            var hero = new Character("유랑객", new Stats { Outer = 7, Light = 9, Con = 4, Wis = 6 }, new[] { sword, mind })
            {
                Realm = Realm.Iryu, InnerPower = 55, Fatigue = 40, Money = 123, InnerInjured = true,
            };
            hero.GainMastery(sword, 160);
            hero.MasteryCaps[sword] = 7;
            hero.GainMastery(sword, 100000);
            hero.PassForm(sword);
            hero.Train(Training.Reading); // 오성 진척도 25
            var state = new GameState(hero) { Sect = "청운검문", Disciple = true };
            state.Set("호감.윤백산", 7);
            state.SetQuest("대사형의 장부", 2, "단서 하나");
            state.Expelled.Add("흑사방");

            string json = SaveGame.Write(state, "{\"ink\":1}");

            var loaded = new GameState(new Character("?", new Stats()));
            string story = SaveGame.Read(json, loaded, n => arts.TryGetValue(n, out var a) ? a : null);
            var h = loaded.Hero;

            Assert.AreEqual("{\"ink\":1}", story);
            Assert.AreEqual("유랑객", h.Name);
            Assert.AreEqual(9, h.Stats.Light);
            Assert.AreEqual(Realm.Iryu, h.Realm);
            Assert.AreEqual((55, 55, 123), (h.InnerPower, h.Fatigue, h.Money));
            Assert.IsTrue(h.InnerInjured);
            CollectionAssert.AreEqual(new[] { sword, mind }, h.Arts);
            Assert.AreEqual(hero.Mastery(sword), h.Mastery(sword));
            Assert.AreEqual(7, h.MasteryCaps[sword]);
            Assert.AreEqual(7, loaded.Get("호감.윤백산"));
            Assert.AreEqual(new[] { "단서 하나" }, loaded.FindQuest("대사형의 장부").Notes);
            Assert.AreEqual("청운검문", loaded.Sect);
            Assert.IsTrue(loaded.Disciple);
            CollectionAssert.Contains(loaded.Expelled, "흑사방");
        }

        [Test]
        public void NoSectStaysNull()
        {
            var state = new GameState(new Character("나", new Stats()));
            var loaded = new GameState(new Character("?", new Stats()));
            SaveGame.Read(SaveGame.Write(state, ""), loaded, _ => null);
            Assert.IsNull(loaded.Sect);
        }

        // 한 번만 고를 수 있는 선택지는 저장·불러오기 뒤에도 다시 나오지 않는다.
        [Test]
        public void StoryStateSurvivesSave()
        {
            const string src = "=== once ===\n* [first] -> END\n+ [always] -> END\n";
            var json = DialogueTests.Compile(src);
            var state = new GameState(new Character("나", new Stats()));

            var d = new Dialogue(json, state);
            d.Start("once");
            DialogueTests.Drain(d);
            d.Choose(0);
            DialogueTests.Drain(d);

            var reloaded = new Dialogue(json, state);
            reloaded.LoadState(d.SaveState());
            reloaded.Start("once");
            DialogueTests.Drain(reloaded);

            CollectionAssert.AreEqual(new[] { "always" }, reloaded.Choices);
        }
    }
}
