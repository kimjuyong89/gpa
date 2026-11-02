using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 기예 10종: 쓸수록 오르고, 계통 무공의 위력·명중과 의술·독술을 돕는다.
    public class SkillTests
    {
        static MartialArt Sword(int need = 0)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 100;
            a.skill = SkillKind.Sword;
            a.skillNeed = need;
            return a;
        }

        static Character Hero(params MartialArt[] arts) => new("나", new Stats { Light = 10, Con = 20 }, arts);

        static Battle Duel(Character h, out Combatant hero, out Combatant foe)
        {
            hero = h.ToCombatant();
            foe = new Combatant("적", false, new Stats { Con = 50 }, new[] { Sword() });
            return new Battle(new[] { hero }, new[] { foe }, new System.Random(1)) { DamageSpread = 0 };
        }

        [Test]
        public void SkillsStartAtZeroAndGrowSlowerAsTheyRise()
        {
            var h = Hero();
            Assert.AreEqual(0, h.Skill(SkillKind.Sword));
            h.GainSkill(SkillKind.Sword, 10);
            Assert.AreEqual(1, h.Skill(SkillKind.Sword), "처음엔 10번에 1");

            int uses = 0;
            while (h.Skill(SkillKind.Sword) < 2)
            {
                h.GainSkill(SkillKind.Sword, 1);
                uses++;
            }
            Assert.Greater(uses, 10, "오를수록 느리다");
        }

        [Test]
        public void SkillIsCapped()
        {
            var h = Hero();
            h.GainSkill(SkillKind.Palm, 1000000);
            Assert.AreEqual(Character.MaxSkill, h.Skill(SkillKind.Palm));
        }

        [Test]
        public void WisdomSpeedsUpGrowth()
        {
            var slow = new Character("나", new Stats());
            var fast = new Character("나", new Stats { Wis = 20 });
            slow.GainSkill(SkillKind.Fist, 7);
            fast.GainSkill(SkillKind.Fist, 7);
            Assert.AreEqual(0, slow.Skill(SkillKind.Fist));
            Assert.AreEqual(1, fast.Skill(SkillKind.Fist));
        }

        [Test]
        public void UsingAnArtTrainsItsSkill()
        {
            var art = Sword();
            var h = Hero(art);
            var b = Duel(h, out var hero, out var foe);
            for (int i = 0; i < 10; i++)
            {
                b.BeginTurn();
                if (b.Current == hero) b.UseArt(art, foe);
                else b.AutoAct();
            }
            b.Finish();
            int uses = hero.SkillUses[SkillKind.Sword];
            Assert.Greater(uses, 0);
            h.ApplyBattle(hero);
            h.GainSkill(SkillKind.Sword, 10 - uses);
            Assert.AreEqual(1, h.Skill(SkillKind.Sword), "전투에서 쓴 횟수가 쌓였다");
        }

        [Test]
        public void SparringTrainsTheSkill()
        {
            var art = Sword();
            var h = Hero(art);
            for (int i = 0; i < 4; i++)
            {
                h.Train(Training.Sparring, art);
                h.Fatigue = 0;
            }
            Assert.AreEqual(1, h.Skill(SkillKind.Sword));
        }

        [Test]
        public void SkillRaisesPowerOfItsLine()
        {
            var art = Sword();
            var h = Hero(art);
            var plain = h.ToCombatant();
            h.GainSkill(SkillKind.Sword, 1000000);
            var master = h.ToCombatant();
            Assert.AreEqual(1f, plain.SkillMultiplier(art));
            Assert.AreEqual(1.5f, master.SkillMultiplier(art), 0.001f);

            var other = ScriptableObject.CreateInstance<MartialArt>();
            other.skill = SkillKind.Fist;
            Assert.AreEqual(1f, master.SkillMultiplier(other));
        }

        [Test]
        public void SecretArtsAskForSkill()
        {
            var secret = Sword(need: 10);
            var h = Hero(secret);
            var b = Duel(h, out _, out var foe);
            b.BeginTurn();
            Assert.IsFalse(b.CanUse(secret, foe), "검술 10 필요");

            h.GainSkill(SkillKind.Sword, 200);
            Assert.GreaterOrEqual(h.Skill(SkillKind.Sword), 10);
            b = Duel(h, out _, out foe);
            b.BeginTurn();
            Assert.IsTrue(b.CanUse(secret, foe));
        }

        [Test]
        public void EnemiesIgnoreSkillRules()
        {
            var secret = Sword(need: 50);
            var foe = new Combatant("적", false, new Stats(), new[] { secret });
            Assert.IsTrue(foe.MeetsSkill(secret));
            Assert.AreEqual(1f, foe.SkillMultiplier(secret));
        }

        [Test]
        public void MedicineStrengthensPotions()
        {
            var pill = ScriptableObject.CreateInstance<ItemData>();
            pill.kind = ItemKind.Consumable;
            pill.heal = 40;
            int Healed(int uses)
            {
                var h = Hero();
                h.GainSkill(SkillKind.Medicine, uses);
                h.Bag.Add(pill);
                var b = Duel(h, out var hero, out _);
                b.Bag = h.Bag;
                b.BeginTurn();
                hero.Hp = 1;
                b.UseItem(pill, hero);
                return hero.Hp - 1;
            }
            Assert.Greater(Healed(1000), Healed(0));
        }

        [Test]
        public void FirstAidHealsAnAllyAndCostsInner()
        {
            var b = Duel(Hero(), out var hero, out _);
            b.BeginTurn();
            Assert.IsFalse(b.CanFirstAid(hero), "다치지 않으면 못 쓴다");
            hero.Hp = 1;
            int inner = hero.Inner;
            Assert.IsTrue(b.CanFirstAid(hero));
            b.FirstAid(hero);
            Assert.Greater(hero.Hp, 1);
            Assert.AreEqual(inner - Battle.ActionCost, hero.Inner);
            Assert.IsNull(b.Current, "턴을 쓴다");
            Assert.AreEqual(1, hero.SkillUses[SkillKind.Medicine]);
        }

        [Test]
        public void ThrowingPoisonInflictsItAndTrains()
        {
            int poisoned = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                var hero = Hero().ToCombatant();
                var foe = new Combatant("적", false, new Stats { Con = 50 }, new[] { Sword() });
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(seed));
                b.BeginTurn();
                b.ThrowPoison(foe);
                if (foe.Has(StatusKind.Poison)) poisoned++;
                Assert.AreEqual(1, hero.SkillUses[SkillKind.Poison]);
            }
            Assert.That(poisoned, Is.InRange(60, 125), "기본 약 30%");
        }

        [Test]
        public void ActionsNeedInner()
        {
            var b = Duel(Hero(), out var hero, out var foe);
            b.BeginTurn();
            hero.Hp = 1;
            hero.Inner = 0;
            Assert.IsFalse(b.CanFirstAid(hero));
            Assert.IsFalse(b.CanThrowPoison(foe));
        }

        [Test]
        public void AntidoteLowersPoisonChance()
        {
            var h = Hero();
            h.GainSkill(SkillKind.Antidote, 1000000);
            var c = h.ToCombatant();
            Assert.AreEqual(0.4f, c.PoisonGuard, 0.001f);
            Assert.AreEqual(1f, Hero().ToCombatant().PoisonGuard);
        }

        [Test]
        public void TheDemonMirrorsSkills()
        {
            var h = Hero();
            h.GainSkill(SkillKind.Sword, 200);
            Assert.AreEqual(h.Skill(SkillKind.Sword), h.InnerDemon().SkillOf(SkillKind.Sword));
        }

        [Test]
        public void SkillsSurviveSaving()
        {
            var state = new GameState(Hero());
            state.Hero.GainSkill(SkillKind.Kick, 15);
            var loaded = new GameState(new Character("?", new Stats()));
            SaveGame.Read(SaveGame.Write(state, "{}"), loaded, _ => null);
            Assert.AreEqual(state.Hero.Skill(SkillKind.Kick), loaded.Hero.Skill(SkillKind.Kick));
            loaded.Hero.GainSkill(SkillKind.Kick, 6); // 1에서 2까지 110, 이미 50이 쌓여 있다
            Assert.AreEqual(2, loaded.Hero.Skill(SkillKind.Kick), "진척도 이어진다");
        }

        [Test]
        public void TrainingSitesRaiseBareHandSkills()
        {
            var h = Hero();
            var run = new Circulation(Effort.Steady, 0, 5, 5, false, 0, new System.Random(1));
            for (int t = 0; t < 60 * 60 && run.Result == CirculationEnd.Ongoing; t++) run.Tick(1 / 60f, run.Speed < run.WantNow);
            for (int i = 0; i < 4; i++) h.ApplyCirculation(run, StatKind.Outer);
            Assert.Greater(h.Skill(SkillKind.Fist), 0, "바위는 권법");
        }

        [Test]
        public void ArtDataHasSkills()
        {
            MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"Assets/_Project/Data/MartialArts/{file}.asset");
            Assert.AreEqual(SkillKind.Sword, Art("Jeonjingeom").skill);
            Assert.AreEqual(SkillKind.Hidden, Art("HeuksaBisu").skill);
            Assert.AreEqual(SkillKind.Finger, Art("HeuksaSwaehyeol").skill);
            Assert.Greater(Art("CheongunNagilgeom").skillNeed, 0);
            Assert.Greater(Art("HeuksaSwaehyeol").skillNeed, 0);
            Assert.AreEqual(10, System.Enum.GetValues(typeof(SkillKind)).Cast<SkillKind>().Count(k => k != SkillKind.None));
        }
    }
}
