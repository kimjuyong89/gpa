using GPA.Combat;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class GuardTests
    {
        static MartialArt Outer(int guard)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 10;
            a.guard = guard;
            return a;
        }

        static MartialArt Step(int guard, int evasion, int turns)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Movement;
            a.guard = guard;
            a.evasionBuff = evasion;
            a.buffTurns = turns;
            return a;
        }

        static Battle Duel(MartialArt art, out Combatant hero, out Combatant foe)
        {
            hero = new Combatant("가", true, new Stats { Light = 20, Con = 50 }, new[] { art, Outer(0) });
            foe = new Combatant("나", false, new Stats { Light = 10, Con = 50 }, new[] { Outer(0) });
            return new Battle(new[] { hero }, new[] { foe }, new System.Random(1)) { DamageSpread = 0 };
        }

        [Test]
        public void ArtGuardLastsUntilTheNextOwnTurn()
        {
            var art = Outer(20);
            var b = Duel(art, out var hero, out var foe);
            b.BeginTurn();
            Assert.AreEqual(1f, hero.DamageTaken);
            b.UseArt(art, foe);
            Assert.AreEqual(20, hero.Guard);
            Assert.AreEqual(0.8f, hero.DamageTaken, 0.001f);

            b.BeginTurn(); // 다음 행동자는 빠른 주인공 자신이다
            Assert.AreSame(hero, b.Current);
            Assert.AreEqual(0, hero.Guard, "자기 턴이 오면 풀린다");
        }

        [Test]
        public void GuardReducesDamageTaken()
        {
            var a = Outer(0);
            int Hit(int guard)
            {
                var t = new Combatant("나", false, new Stats { Con = 50 }, new[] { a }) { ArtGuard = guard };
                var hero = new Combatant("가", true, new Stats { Light = 10 }, new[] { a });
                var b = new Battle(new[] { hero }, new[] { t }, new System.Random(1)) { DamageSpread = 0 };
                b.BeginTurn();
                int hp = t.Hp;
                b.UseArt(a, t);
                return hp - t.Hp;
            }
            Assert.AreEqual(Hit(0) * 0.5f, Hit(50), 1);
        }

        [Test]
        public void StepGivesGuardAndEvasionForItsTurns()
        {
            var step = Step(10, 15, 2);
            var b = Duel(step, out var hero, out _);
            b.BeginTurn();
            float before = hero.EvasionChance;
            b.UseArt(step);
            Assert.AreEqual(10, hero.Guard);
            Assert.AreEqual(before + 0.15f, hero.EvasionChance, 0.001f, "회피는 확률에 그대로 더한다");

            b.BeginTurn();
            Assert.AreEqual(10, hero.Guard, "2턴 지속: 한 턴 지나도 남는다");
            b.Meditate();
            b.BeginTurn();
            if (b.Current != hero) { b.AutoAct(); b.BeginTurn(); }
            Assert.AreEqual(0, hero.Guard);
            Assert.AreEqual(before, hero.EvasionChance, 0.001f);
        }

        [Test]
        public void StepAndArtGuardAddUpUnderTheCap()
        {
            var c = new Combatant("가", true, new Stats(), new[] { Outer(0) }) { ArtGuard = 15, MoveGuard = 10 };
            Assert.AreEqual(0.75f, c.DamageTaken, 0.001f);
            c.Gear = new[] { MakeArmor(50) };
            Assert.AreEqual(0.4f, c.DamageTaken, 0.001f, "장비와 합쳐 60%까지");
        }

        static ItemData MakeArmor(int defense)
        {
            var i = ScriptableObject.CreateInstance<ItemData>();
            i.kind = ItemKind.Clothes;
            i.defense = defense;
            return i;
        }

        [Test]
        public void EvasionIsCapped()
        {
            var c = new Combatant("가", true, new Stats { Light = 40 }, new[] { Outer(0) }) { EvasionBuff = 45 };
            Assert.AreEqual(0.5f, c.EvasionChance, 0.001f);
        }
    }
}
