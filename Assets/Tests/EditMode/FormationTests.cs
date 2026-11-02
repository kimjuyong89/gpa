using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    public class FormationTests
    {
        static MartialArt Melee()
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 10;
            return a;
        }

        static Combatant Unit(string name, bool ally, Row row, int lane, MartialArt art = null) =>
            new(name, ally, new Stats { Light = ally ? 10 : 1, Con = 5 }, new[] { art ?? Melee() }, row) { Lane = lane };

        static GameState State() => new(new Character("나", new Stats()));

        [Test]
        public void SameLaneFrontProtectsTheBackRow()
        {
            var art = Melee();
            var hero = Unit("가", true, Row.Front, 1, art);
            var guard = Unit("앞", false, Row.Front, 0);
            var shielded = Unit("뒤", false, Row.Back, 0);
            var exposed = Unit("옆", false, Row.Back, 2);
            var b = new Battle(new[] { hero }, new[] { guard, shielded, exposed });
            b.BeginTurn();
            Assert.IsFalse(b.CanUse(art, shielded), "같은 가로줄의 전열이 막는다");
            Assert.IsTrue(b.CanUse(art, exposed), "앞이 빈 줄은 뚫려 있다");
            guard.TakeDamage(9999);
            Assert.IsTrue(b.CanUse(art, shielded), "전열이 쓰러지면 닿는다");
        }

        [Test]
        public void UnitsThatShareACellSpreadOut()
        {
            var units = Enumerable.Range(0, 6).Select(i => Unit("적" + i, false, Row.Front, 0)).ToList();
            var b = new Battle(new[] { Unit("가", true, Row.Front, 0) }, units);
            Assert.AreEqual(6, units.Select(u => (u.Row, u.Lane)).Distinct().Count());
            Assert.AreEqual(3, units.Count(u => u.Row == Row.Front));
        }

        [Test]
        public void SwappingRowsTradesPlacesWithTheUnitThere()
        {
            var step = ScriptableObject.CreateInstance<MartialArt>();
            step.kind = ArtKind.Movement;
            step.swapsRow = true;
            var hero = new Combatant("가", true, new Stats { Light = 10, Con = 5 }, new[] { step }, Row.Front) { Lane = 1 };
            var friend = Unit("동료", true, Row.Back, 1);
            var b = new Battle(new[] { hero, friend }, new[] { Unit("적", false, Row.Front, 0) });
            b.BeginTurn();
            Assert.AreSame(hero, b.Current);
            b.UseArt(step);
            Assert.AreEqual(Row.Back, hero.Row);
            Assert.AreEqual(Row.Front, friend.Row);
        }

        [Test]
        public void PartyStopsAtThreeCompanions()
        {
            var s = State();
            Assert.IsTrue(s.Recruit("가"));
            Assert.IsTrue(s.Recruit("나"));
            Assert.IsTrue(s.Recruit("다"));
            Assert.IsTrue(s.PartyFull);
            Assert.IsFalse(s.Recruit("라"));
            Assert.AreEqual(0, s.Get("동료.라"));
            Assert.IsTrue(s.Recruit("가"), "이미 일행이면 그대로다");

            s.Dismiss("나");
            Assert.AreEqual(2, s.CompanionCount);
            Assert.IsTrue(s.Recruit("라"));
        }

        [Test]
        public void FormationGivesEveryoneTheirOwnCell()
        {
            var f = State().Formation(new[] { "가", "나", "다" });
            Assert.AreEqual(1, f[GameState.HeroKey], "주인공은 전열 가운데");
            Assert.AreEqual(4, f["가"], "첫 동료는 그 뒤");
            Assert.AreEqual(4, f.Values.Distinct().Count());
        }

        [Test]
        public void MovingOntoSomeoneSwapsCellsAndPersists()
        {
            var s = State();
            string[] party = { "가" };
            s.MoveInFormation("가", 1, party);
            var f = s.Formation(party);
            Assert.AreEqual(1, f["가"]);
            Assert.AreEqual(4, f[GameState.HeroKey], "밀려난 주인공은 동료가 있던 칸으로");

            s.MoveInFormation(GameState.HeroKey, 5, party);
            Assert.AreEqual(5, s.Formation(party)[GameState.HeroKey]);
            Assert.AreEqual(1, s.Formation(party)["가"]);
        }

        [Test]
        public void DismissedCompanionsCellIsFreed()
        {
            var s = State();
            s.MoveInFormation("가", 0, new[] { "가" });
            var f = s.Formation(new[] { "나" });
            Assert.AreEqual(2, f.Count);
            Assert.IsFalse(f.ContainsKey("가"));
        }

        [Test]
        public void EveryBattleFitsTheGrid()
        {
            var battles = AssetDatabase.FindAssets("t:BattleData")
                .Select(g => AssetDatabase.LoadAssetAtPath<BattleData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.IsTrue(battles.Any(b => b.enemies.Count == 6), "6인 전투가 있다");
            foreach (var b in battles)
            {
                Assert.LessOrEqual(b.enemies.Count, 6, b.id);
                var battle = new Battle(new[] { Unit("가", true, Row.Front, 1) }, b.enemies.Select(e => e.Create()));
                Assert.AreEqual(b.enemies.Count, battle.Enemies.Select(e => (e.Row, e.Lane)).Distinct().Count(), b.id);
            }
        }
    }
}
