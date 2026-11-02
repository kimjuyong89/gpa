using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 무공 체계: 초식, 연계, 계통 고유 역할, 등급.
    public class MoveTests
    {
        static Move M(string name, int stage = 1, int power = 10, int hits = 1, int cost = 0) =>
            new() { name = name, stage = stage, power = power, hits = hits, innerCost = cost };

        static MartialArt Art(SkillKind skill, params Move[] moves)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.displayName = "시험";
            a.skill = skill;
            a.moves = moves;
            return a;
        }

        static Combatant Hero(MartialArt art, int mastery = 10) =>
            new("가", true, new Stats { Outer = 10, Light = 20, Con = 30 }, new[] { art }, Row.Front, false, 0,
                new System.Collections.Generic.Dictionary<MartialArt, int> { [art] = mastery });

        static Combatant Foe(string name = "적", Row row = Row.Front, int lane = 0) =>
            new(name, false, new Stats { Con = 40 }, new[] { Art(SkillKind.None, M("적")) }, row) { Lane = lane };

        static Battle Fight(Combatant hero, params Combatant[] foes) =>
            new(new[] { hero }, foes, new System.Random(1)) { DamageSpread = 0 };

        [Test]
        public void ArtsWithoutMovesActAsOneMove()
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.displayName = "옛 무공";
            a.power = 12;
            a.innerCost = 3;
            Assert.AreEqual(1, a.Moves.Length);
            Assert.AreEqual(12, a.Moves[0].power);
            Assert.AreSame(a.Moves[0], a.Moves[0]);
        }

        [Test]
        public void MovesUnlockWithMastery()
        {
            var art = Art(SkillKind.None, M("일"), M("이", stage: 3));
            var hero = Hero(art, mastery: 2);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            Assert.IsTrue(b.CanUse(art, art.Moves[0], foe));
            Assert.IsFalse(b.CanUse(art, art.Moves[1], foe), "3성이 필요하다");
        }

        [Test]
        public void MoveUsesItsOwnCostAndCooldown()
        {
            var fast = M("일", cost: 5);
            var slow = M("이", cost: 10);
            slow.cooldown = 2;
            var art = Art(SkillKind.None, fast, slow);
            var hero = Hero(art, mastery: 1);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            int inner = hero.Inner;
            Assert.IsTrue(b.UseMove(art, fast, foe));
            Assert.AreEqual(inner - 5, hero.Inner);
            Assert.AreEqual(0, hero.Cooldown(slow));
            b.BeginTurn();
            b.UseMove(art, slow, foe);
            Assert.Greater(hero.Cooldown(slow), 0);
            Assert.AreEqual(0, hero.Cooldown(fast), "대기는 초식마다 따로다");
        }

        [Test]
        public void ComboGrowsInOrderAndBreaksOtherwise()
        {
            var art = Art(SkillKind.None, M("일"), M("이"), M("삼"));
            var hero = Hero(art);
            var foe = Foe();
            var b = Fight(hero, foe);

            b.BeginTurn();
            Assert.IsFalse(hero.ComboNext(art, art.Moves[1]));
            b.UseMove(art, art.Moves[0], foe);
            Assert.AreEqual(1f, hero.ComboMultiplier(art));

            b.BeginTurn();
            Assert.IsTrue(hero.ComboNext(art, art.Moves[1]));
            b.UseMove(art, art.Moves[1], foe);
            Assert.AreEqual(1.1f, hero.ComboMultiplier(art), 0.001f);

            b.BeginTurn();
            b.UseMove(art, art.Moves[2], foe);
            Assert.AreEqual(1.2f, hero.ComboMultiplier(art), 0.001f);

            b.BeginTurn();
            b.Meditate();
            b.BeginTurn();
            Assert.IsFalse(hero.ComboNext(art, art.Moves[1]), "다른 행동을 하면 끊긴다");
            b.UseMove(art, art.Moves[0], foe);
            Assert.AreEqual(1f, hero.ComboMultiplier(art));
        }

        [Test]
        public void OutOfOrderBreaksTheCombo()
        {
            var art = Art(SkillKind.None, M("일"), M("이"), M("삼"));
            var hero = Hero(art);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], foe);
            b.BeginTurn();
            b.UseMove(art, art.Moves[2], foe);
            Assert.AreEqual(1f, hero.ComboMultiplier(art));
        }

        [Test]
        public void SwordHasTheStrongestCombo()
        {
            var sword = Art(SkillKind.Sword, M("일"), M("이"));
            var hero = Hero(sword);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            b.UseMove(sword, sword.Moves[0], foe);
            b.BeginTurn();
            b.UseMove(sword, sword.Moves[1], foe);
            Assert.AreEqual(1.25f, hero.ComboMultiplier(sword), 0.001f);
        }

        [Test]
        public void BladeIgnoresPartOfDefense()
        {
            int Hit(SkillKind skill)
            {
                var art = Art(skill, M("일", power: 100));
                var hero = Hero(art, mastery: 1);
                var foe = Foe();
                foe.ArtGuard = 50;
                var b = Fight(hero, foe);
                b.BeginTurn();
                int hp = foe.Hp;
                b.UseMove(art, art.Moves[0], foe);
                return hp - foe.Hp;
            }
            Assert.Greater(Hit(SkillKind.Blade), Hit(SkillKind.None));
        }

        [Test]
        public void PalmSplashesTheWholeRow()
        {
            var art = Art(SkillKind.Palm, M("일", power: 100));
            var hero = Hero(art, mastery: 1);
            Combatant main = Foe("가운데", lane: 1), side = Foe("옆", lane: 0), back = Foe("뒤", Row.Back, 2);
            var b = Fight(hero, main, side, back);
            b.BeginTurn();
            int full = main.Hp;
            b.UseMove(art, art.Moves[0], main);
            int hit = full - main.Hp, splash = side.MaxHp - side.Hp;
            Assert.Greater(splash, 0, "같은 열은 함께 맞는다");
            Assert.Less(splash, hit);
            Assert.AreEqual(back.MaxHp, back.Hp, "다른 열은 맞지 않는다");
        }

        [Test]
        public void FingerDrainsInner()
        {
            var art = Art(SkillKind.Finger, M("일"));
            var hero = Hero(art);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            int inner = foe.Inner;
            b.UseMove(art, art.Moves[0], foe);
            Assert.Less(foe.Inner, inner);
        }

        [Test]
        public void FistHitsGrowStronger()
        {
            var art = Art(SkillKind.Fist, M("일", power: 100, hits: 3));
            var hero = Hero(art, mastery: 1);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], foe);
            var hits = b.Events.Where(e => e.Kind == EventKind.Hit).Select(e => e.Amount).ToList();
            Assert.AreEqual(3, hits.Count);
            Assert.Greater(hits[2], hits[0]);
        }

        [Test]
        public void KickPushesTheTargetBack()
        {
            var art = Art(SkillKind.Kick, M("일"));
            var hero = Hero(art);
            Combatant front = Foe("앞", Row.Front, 1), behind = Foe("뒤", Row.Back, 1);
            var b = Fight(hero, front, behind);
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], front);
            Assert.AreEqual(Row.Back, front.Row);
            Assert.AreEqual(Row.Front, behind.Row, "뒤에 있던 적이 앞으로 나온다");
        }

        [Test]
        public void HiddenWeaponsLandMoreOftenAndCritMore()
        {
            int Dodges(SkillKind skill)
            {
                int dodged = 0;
                for (int seed = 0; seed < 300; seed++)
                {
                    var art = Art(skill, M("일"));
                    var hero = Hero(art, mastery: 1);
                    var foe = Foe();
                    foe.EvasionBuff = 40;
                    var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(seed));
                    b.BeginTurn();
                    b.UseMove(art, art.Moves[0], foe);
                    if (b.Events.Any(e => e.Kind == EventKind.Dodge)) dodged++;
                }
                return dodged;
            }
            Assert.Less(Dodges(SkillKind.Hidden), Dodges(SkillKind.None));
        }

        [Test]
        public void GradeAsksForARealmButSupremeDoesNot()
        {
            var art = Art(SkillKind.None, M("일"));
            art.grade = ArtGrade.Second;
            var low = Hero(art);
            low.Skills = new System.Collections.Generic.Dictionary<SkillKind, int>();
            Assert.IsFalse(low.MeetsGrade(art), "삼류는 이류 무공을 쓰지 못한다");
            art.grade = ArtGrade.Supreme;
            Assert.IsTrue(low.MeetsGrade(art), "절세 무공은 기연으로만 얻으니 경지를 묻지 않는다");
            art.grade = ArtGrade.Second;
            Assert.IsTrue(Foe().MeetsGrade(art), "적은 따지지 않는다");
        }

        [Test]
        public void AutoActTakesTheComboWhenItCan()
        {
            var art = Art(SkillKind.None, M("일"), M("이"), M("삼"));
            var hero = Hero(art);
            var foe = Foe();
            var b = Fight(hero, foe);
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], foe);
            b.BeginTurn();
            b.AutoAct();
            Assert.AreEqual(1.1f, hero.ComboMultiplier(art), 0.001f);
        }

        const string Data = "Assets/_Project/Data/";

        static MartialArt[] AllArts => AssetDatabase.FindAssets("t:MartialArt", new[] { Data + "MartialArts", Data + "Resources/Arts" })
            .Select(g => AssetDatabase.LoadAssetAtPath<MartialArt>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        [Test]
        public void EveryOuterArtFollowsItsGrade()
        {
            int[] moves = { 3, 3, 4, 4, 5 };
            foreach (var a in AllArts.Where(a => a.kind == ArtKind.Outer && a.moves.Length > 0))
            {
                Assert.AreEqual(moves[(int)a.grade], a.moves.Length, a.displayName);
                CollectionAssert.AreEqual(a.moves.Select(m => m.name), a.forms, a.displayName);
                Assert.AreEqual(a.power, a.moves[0].power, a.displayName + ": 첫 초식이 무공의 기준이다");
                Assert.AreEqual(a.innerCost, a.moves[0].innerCost, a.displayName);
                Assert.AreEqual(1, a.moves[0].stage, a.displayName);
                Assert.AreNotEqual(SkillKind.None, a.skill, a.displayName);
            }
        }

        [Test]
        public void EveryFamilyHasAnArt()
        {
            foreach (var k in new[] { SkillKind.Sword, SkillKind.Blade, SkillKind.Palm, SkillKind.Finger, SkillKind.Fist, SkillKind.Kick, SkillKind.Hidden })
                Assert.IsTrue(AllArts.Any(a => a.skill == k), k.ToString());
        }
    }
}
