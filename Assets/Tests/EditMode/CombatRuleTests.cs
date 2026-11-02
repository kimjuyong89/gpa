using System.Linq;
using GPA.Combat;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    // 전투 규칙: 위력 보너스 덧셈, 기세와 절초, 적 성향과 예고.
    public class CombatRuleTests
    {
        static MartialArt Art(params Move[] moves)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.displayName = "시험";
            a.moves = moves;
            return a;
        }

        static Move M(string name, int power = 10, int hits = 1, int guard = 0) => new() { name = name, power = power, hits = hits, guard = guard };

        static Combatant Unit(string name, bool ally, MartialArt art, int light = 5, Temper temper = Temper.Plain, Row row = Row.Front, int lane = 0) =>
            new(name, ally, new Stats { Outer = 10, Light = light, Con = 40 }, new[] { art }, row) { Temper = temper, Lane = lane };

        [Test]
        public void BonusesAddInsteadOfMultiplying()
        {
            int Hit(params ItemData[] gear)
            {
                var art = Art(M("일", 100));
                art.weapon = WeaponType.Sword;
                var hero = Unit("가", true, art, 20);
                hero.Gear = gear;
                var foe = Unit("적", false, Art(M("적")));
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1)) { DamageSpread = 0 };
                b.BeginTurn();
                int hp = foe.Hp;
                b.UseMove(art, art.Moves[0], foe);
                return hp - foe.Hp;
            }
            var sword = ScriptableObject.CreateInstance<ItemData>();
            sword.kind = ItemKind.Weapon;
            sword.attack = 10;
            sword.weapon = WeaponType.Sword;
            float ratio = Hit(sword) / (float)Hit();
            Assert.AreEqual(1.2f, ratio, 0.02f, "공격 10% + 일치 10%는 곱해서 1.21이 아니라 더해서 1.2다");
        }

        [Test]
        public void HitsBuildMomentumOnBothSides()
        {
            var art = Art(M("일"));
            var hero = Unit("가", true, art, 20);
            var foe = Unit("적", false, Art(M("적")));
            var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1));
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], foe);
            Assert.AreEqual(Combatant.MomentumOnHit, hero.Momentum);
            Assert.AreEqual(Combatant.MomentumOnHurt, foe.Momentum);
        }

        [Test]
        public void MomentumIsCapped()
        {
            var c = Unit("가", true, Art(M("일")));
            c.Momentum = 99;
            c.GainMomentum(50);
            Assert.AreEqual(Combatant.MaxMomentum, c.Momentum);
        }

        [Test]
        public void BurstSpendsMomentumAndHitsHarderOnce()
        {
            int Hit(bool burst)
            {
                var art = Art(M("일", 100));
                var hero = Unit("가", true, art, 20);
                hero.Momentum = 60;
                var foe = Unit("적", false, Art(M("적")));
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1)) { DamageSpread = 0 };
                b.BeginTurn();
                if (burst)
                {
                    Assert.IsTrue(b.ArmBurst());
                    Assert.AreEqual(10, hero.Momentum);
                    Assert.AreSame(hero, b.Current, "준비는 턴을 쓰지 않는다");
                }
                int hp = foe.Hp;
                b.UseMove(art, art.Moves[0], foe);
                Assert.IsFalse(hero.Burst, "한 번 쓰면 끝이다");
                return hp - foe.Hp;
            }
            Assert.AreEqual(1.75f, Hit(true) / (float)Hit(false), 0.05f);
        }

        [Test]
        public void BurstNeedsEnoughMomentum()
        {
            var art = Art(M("일"));
            var hero = Unit("가", true, art, 20);
            hero.Momentum = 49;
            var b = new Battle(new[] { hero }, new[] { Unit("적", false, Art(M("적"))) });
            b.BeginTurn();
            Assert.IsFalse(b.ArmBurst());
        }

        [Test]
        public void EnemiesTelegraphTheirNextMove()
        {
            var art = Art(M("일"));
            var hero = Unit("가", true, Art(M("주")), 20);
            var foe = Unit("적", false, art);
            new Battle(new[] { hero }, new[] { foe }, new System.Random(1));
            Assert.IsNotNull(foe.Intent);
            Assert.AreSame(art, foe.Intent.Art);
            Assert.AreSame(hero, foe.Intent.Target);
        }

        [Test]
        public void EnemyDoesWhatItTelegraphed()
        {
            var a = Art(M("일"), M("이"));
            var hero = Unit("가", true, Art(M("주")), 1);
            var foe = new Combatant("적", false, new Stats { Outer = 10, Light = 20, Con = 40 }, new[] { a, Art(M("딴")) });
            var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(3));
            var planned = foe.Intent;
            b.BeginTurn();
            Assert.AreSame(foe, b.Current);
            b.AutoAct();
            Assert.IsTrue(b.Events.Any(e => e.Kind == EventKind.Act && e.Text.EndsWith(planned.Move.name)));
            Assert.AreNotSame(planned, foe.Intent, "다음 예고가 새로 나온다");
        }

        [Test]
        public void IntentChangesWhenTheTargetFalls()
        {
            var hero = Unit("가", true, Art(M("주")), 20);
            var friend = Unit("벗", true, Art(M("주")), 20, lane: 1);
            var foe = Unit("적", false, Art(M("일")));
            var b = new Battle(new[] { hero, friend }, new[] { foe }, new System.Random(1));
            var first = foe.Intent.Target;
            first.TakeDamage(99999);
            b.BeginTurn();
            b.Meditate();
            Assert.AreNotSame(first, foe.Intent.Target);
        }

        [Test]
        public void AggressiveTakesItsStrongestMove()
        {
            var a = Art(M("약", 5), M("강", 30));
            var foe = new Combatant("적", false, new Stats(), new[] { a }, Row.Front, false, 0,
                new System.Collections.Generic.Dictionary<MartialArt, int> { [a] = 10 }) { Temper = Temper.Aggressive };
            new Battle(new[] { Unit("가", true, Art(M("주"))) }, new[] { foe }, new System.Random(1));
            Assert.AreEqual("강", foe.Intent.Move.name);
        }

        [Test]
        public void GuardedProtectsItselfWhenHurt()
        {
            var a = Art(M("막", 5, guard: 30), M("강", 30));
            var mastery = new System.Collections.Generic.Dictionary<MartialArt, int> { [a] = 10 };
            var foe = new Combatant("적", false, new Stats { Con = 40 }, new[] { a }, Row.Front, false, 0, mastery) { Temper = Temper.Guarded };
            var hero = Unit("가", true, Art(M("주")), 20);
            var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1));
            foe.Hp = 10;
            b.BeginTurn();
            b.Meditate();
            Assert.AreEqual("막", foe.Intent.Move.name);
        }

        [Test]
        public void AssassinGoesForTheBackRow()
        {
            var reach = Art(M("침"));
            reach.reachesBackRow = true;
            var front = Unit("앞", true, Art(M("주")), row: Row.Front, lane: 1);
            var back = Unit("뒤", true, Art(M("주")), row: Row.Back, lane: 1);
            var foe = Unit("적", false, reach, temper: Temper.Assassin);
            new Battle(new[] { front, back }, new[] { foe }, new System.Random(1));
            Assert.AreSame(back, foe.Intent.Target);
        }

        [Test]
        public void EnemyWithMomentumTelegraphsBurst()
        {
            var hero = Unit("가", true, Art(M("주")), 20);
            var foe = Unit("적", false, Art(M("일")));
            var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1));
            foe.Momentum = 60;
            b.BeginTurn();
            b.Meditate();
            Assert.IsTrue(foe.Intent.Burst);
        }

        [Test]
        public void EnemyWithNothingToUseTelegraphsMeditation()
        {
            var costly = Art(new Move { name = "비쌈", power = 10, innerCost = 9999 });
            var foe = Unit("적", false, costly);
            new Battle(new[] { Unit("가", true, Art(M("주"))) }, new[] { foe }, new System.Random(1));
            Assert.IsNull(foe.Intent.Art);
        }
    }
}
