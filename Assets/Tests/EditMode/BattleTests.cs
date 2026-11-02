using System.Linq;
using GPA.Combat;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class BattleTests
    {
        static MartialArt Art(ArtKind kind = ArtKind.Outer, Affinity affinity = Affinity.None, int power = 10, int cost = 0, int cooldown = 0, bool reachesBackRow = false)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.displayName = $"{kind}/{affinity}";
            a.kind = kind;
            a.affinity = affinity;
            a.power = power;
            a.innerCost = cost;
            a.cooldown = cooldown;
            a.reachesBackRow = reachesBackRow;
            return a;
        }

        // 스탯 0이면 회피·치명타가 없어서 결과가 결정적이다.
        static Combatant Unit(string name, bool ally, int agi = 0, int con = 0, Row row = Row.Front, MartialArt[] arts = null) =>
            new(name, ally, new Stats { Light = agi, Con = con }, arts ?? new MartialArt[0], row);

        static Battle Fight(Combatant[] allies, Combatant[] enemies) => new(allies, enemies, new System.Random(1)) { DamageSpread = 0 };

        [Test]
        public void FasterUnitActsMoreOften()
        {
            var fast = Unit("빠름", true, agi: 20);
            var slow = Unit("느림", false, agi: 10);
            var b = Fight(new[] { fast }, new[] { slow });

            var order = Enumerable.Range(0, 6).Select(_ => { var c = b.BeginTurn(); b.Meditate(); return c.Name; }).ToList();

            CollectionAssert.AreEqual(new[] { "빠름", "빠름", "느림", "빠름", "빠름", "느림" }, order);
            CollectionAssert.AreEqual(order, Fight(new[] { Unit("빠름", true, agi: 20) }, new[] { Unit("느림", false, agi: 10) })
                .PreviewOrder(6).Select(c => c.Name));
        }

        [TestCase(Affinity.Kwae, 13)] // 강이 쾌를 이긴다
        [TestCase(Affinity.Yu, 7)]    // 강은 유에 진다
        [TestCase(Affinity.Gang, 10)]
        public void AffinityChangesDamage(Affinity targetStance, int expectedDamage)
        {
            var gang = Art(affinity: Affinity.Gang, power: 10);
            var target = Unit("표적", false);
            target.Stance = targetStance;
            var b = Fight(new[] { Unit("공격", true, agi: 10, arts: new[] { gang }) }, new[] { target });

            b.BeginTurn();
            b.UseArt(gang, target);

            Assert.AreEqual(target.MaxHp - expectedDamage, target.Hp);
        }

        [Test]
        public void AffinityCycle()
        {
            Assert.AreEqual(1.3f, Battle.AffinityMultiplier(Affinity.Kwae, Affinity.Yu));
            Assert.AreEqual(1.3f, Battle.AffinityMultiplier(Affinity.Yu, Affinity.Gang));
            Assert.AreEqual(0.7f, Battle.AffinityMultiplier(Affinity.Kwae, Affinity.Gang));
            Assert.AreEqual(1f, Battle.AffinityMultiplier(Affinity.None, Affinity.Gang));
        }

        [Test]
        public void MeleeCannotReachBackRowWhileFrontStands()
        {
            var melee = Art();
            var thrown = Art(reachesBackRow: true);
            var front = Unit("전열", false);
            var back = Unit("후열", false, row: Row.Back);
            var b = Fight(new[] { Unit("공격", true, agi: 10, arts: new[] { melee, thrown }) }, new[] { front, back });

            b.BeginTurn();
            Assert.IsFalse(b.CanUse(melee, back));
            Assert.IsTrue(b.CanUse(thrown, back));

            front.Hp = 0;
            Assert.IsTrue(b.CanUse(melee, back));
        }

        [Test]
        public void OverspendingInnerCausesQiDeviationThenInjury()
        {
            var heavy = Art(cost: 100);
            var me = Unit("나", true, agi: 10, arts: new[] { heavy });
            var b = Fight(new[] { me }, new[] { Unit("적", false, con: 10) });
            int fullInner = me.MaxInner;

            b.BeginTurn();
            Assert.IsTrue(b.UseArt(heavy, b.Enemies[0]));
            Assert.IsTrue(me.Has(StatusKind.QiDeviation));

            b.Finish();
            Assert.IsTrue(me.InnerInjured);
            Assert.Less(me.MaxInner, fullInner);
        }

        [Test]
        public void PressureSkipsOneTurn()
        {
            var pressed = Unit("점혈", true, agi: 20);
            pressed.Statuses.Add(new Status { Kind = StatusKind.Pressure, Turns = 1 });
            var other = Unit("상대", false, agi: 15);
            var b = Fight(new[] { pressed }, new[] { other });

            Assert.AreSame(other, b.BeginTurn());
            b.Meditate();
            Assert.AreSame(pressed, b.BeginTurn());
        }

        [Test]
        public void BleedDamagesAtTurnStart()
        {
            var me = Unit("나", true, agi: 10);
            me.Statuses.Add(new Status { Kind = StatusKind.Bleed, Turns = 2 });
            var b = Fight(new[] { me }, new[] { Unit("적", false) });

            b.BeginTurn();

            Assert.AreEqual(me.MaxHp - me.MaxHp * 8 / 100, me.Hp);
        }

        [Test]
        public void MeditateRestoresInner()
        {
            var me = Unit("나", true, agi: 10, con: 16); // 최대 내력 100
            me.Inner = 0;
            var b = Fight(new[] { me }, new[] { Unit("적", false) });

            b.BeginTurn(); // 턴 시작 회복 5
            b.Meditate();  // 운기조식 30

            Assert.AreEqual(35, me.Inner);
        }

        [Test]
        public void CooldownBlocksForGivenTurns()
        {
            var secret = Art(power: 1, cooldown: 2);
            var me = Unit("나", true, agi: 20, arts: new[] { secret });
            var dummy = Unit("허수아비", false, agi: 1, con: 100);
            var b = Fight(new[] { me }, new[] { dummy });

            b.BeginTurn();
            Assert.IsTrue(b.UseArt(secret, dummy));
            for (int i = 0; i < 2; i++)
            {
                b.BeginTurn();
                Assert.IsFalse(b.CanUse(secret, dummy), $"사용 후 {i + 1}번째 턴");
                b.Meditate();
            }
            b.BeginTurn();
            Assert.IsTrue(b.CanUse(secret, dummy));
        }

        [Test]
        public void FleeEndsBattleWhenItSucceeds()
        {
            var b = Fight(new[] { Unit("나", true, agi: 40) }, new[] { Unit("적", false, agi: 1) });
            Assert.AreEqual(0.9f, b.FleeChance, 0.001f);

            for (int i = 0; i < 20 && b.Outcome == Outcome.Ongoing; i++)
            {
                b.BeginTurn();
                b.Flee();
            }
            Assert.AreEqual(Outcome.Fled, b.Outcome);
            Assert.IsNull(b.BeginTurn(), "달아나면 더 진행되지 않는다");
        }

        [Test]
        public void SlowPartyFleesRarely()
        {
            var b = Fight(new[] { Unit("나", true, agi: 1) }, new[] { Unit("적", false, agi: 40) });
            Assert.AreEqual(0.2f, b.FleeChance, 0.001f);
        }

        [Test]
        public void MindArtAddsStatusOnHit()
        {
            var venom = Art(ArtKind.Mind);
            venom.onHit = StatusKind.Poison;
            venom.onHitChance = 1;
            venom.onHitTurns = 2;
            var jab = Art();
            var target = Unit("표적", false);
            var b = Fight(new[] { Unit("나", true, agi: 10, arts: new[] { jab, venom }) }, new[] { target });

            b.BeginTurn();
            b.UseArt(jab, target);

            Assert.IsTrue(target.Has(StatusKind.Poison));
        }

        [Test]
        public void AutoActAttacksWhenAffordableElseMeditates()
        {
            var costly = Art(cost: 15);
            var enemy = Unit("적", false, agi: 10, arts: new[] { costly }); // 최대 내력 20
            var hero = Unit("나", true, con: 10);
            var b = Fight(new[] { hero }, new[] { enemy });

            b.BeginTurn();
            b.AutoAct();
            Assert.Less(hero.Hp, hero.MaxHp);

            b.BeginTurn(); // 내력 5 + 1
            b.AutoAct();
            Assert.IsFalse(enemy.Has(StatusKind.QiDeviation));
            Assert.Greater(enemy.Inner, 6);
        }
    }
}
