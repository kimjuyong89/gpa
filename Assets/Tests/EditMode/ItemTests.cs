using System.Linq;
using System.Text.RegularExpressions;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class ItemTests
    {
        static ItemData Make(ItemKind kind, System.Action<ItemData> set = null)
        {
            var i = ScriptableObject.CreateInstance<ItemData>();
            i.kind = kind;
            set?.Invoke(i);
            return i;
        }

        static MartialArt Art(WeaponType weapon = WeaponType.None)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 100;
            a.weapon = weapon;
            return a;
        }

        static Character Hero() => new("나", new Stats { Con = 5, Virtue = 5 });

        [Test]
        public void EquipMovesBetweenBagAndSlot()
        {
            var h = Hero();
            var old = Make(ItemKind.Weapon);
            var better = Make(ItemKind.Weapon);
            h.Bag.Add(old);
            h.Bag.Add(better);
            Assert.IsTrue(h.Equip(old));
            Assert.IsTrue(h.Equip(better));
            Assert.AreSame(better, h.Equipped[0]);
            Assert.AreEqual(1, h.Bag.Count(old), "입던 것은 가방으로 돌아간다");
            Assert.AreEqual(0, h.Bag.Count(better));
            h.Unequip(0);
            Assert.IsNull(h.Equipped[0]);
            Assert.AreEqual(1, h.Bag.Count(better));
            Assert.IsFalse(h.Equip(Make(ItemKind.Boots)), "가방에 없으면 못 입는다");
        }

        [Test]
        public void EverySlotAcceptsItsKindAndTrinketsTakeTwo()
        {
            var h = Hero();
            foreach (var kind in new[] { ItemKind.Weapon, ItemKind.Clothes, ItemKind.Boots, ItemKind.Gloves, ItemKind.Trinket, ItemKind.Trinket })
            {
                var i = Make(kind);
                h.Bag.Add(i);
                Assert.IsTrue(h.Equip(i));
            }
            Assert.AreEqual(6, h.Worn.Count());
            var potion = Make(ItemKind.Consumable);
            h.Bag.Add(potion);
            Assert.IsFalse(h.Equip(potion));
        }

        [Test]
        public void GearStatsGoIntoBattleButNotBaseStats()
        {
            var h = Hero();
            var boots = Make(ItemKind.Boots, i => i.stats.Con = 2);
            h.Bag.Add(boots);
            h.Equip(boots);
            var c = h.ToCombatant();
            Assert.AreEqual(7, c.Stats.Con);
            Assert.AreEqual(50 + 70, c.MaxHp);
            Assert.AreEqual(5, h.Stats.Con);
        }

        [Test]
        public void MatchingWeaponAddsBonusAndDefenseIsCapped()
        {
            var c = new Combatant("가", true, new Stats(), new[] { Art() })
            {
                Gear = new[] { Make(ItemKind.Weapon, i => { i.attack = 10; i.weapon = WeaponType.Sword; }) },
            };
            Assert.AreEqual(1.1f, c.GearMultiplier(Art(WeaponType.Fist)), 0.001f);
            Assert.AreEqual(1.1f * Combatant.WeaponMatch, c.GearMultiplier(Art(WeaponType.Sword)), 0.001f);
            Assert.AreEqual(1.1f, c.GearMultiplier(Art()), 0.001f, "무기가 필요 없는 무공은 일치하지 않는다");

            var d = new Combatant("나", true, new Stats(), new[] { Art() }) { Gear = new[] { Make(ItemKind.Clothes, i => i.defense = 90) } };
            Assert.AreEqual(0.4f, d.DamageTaken, 0.001f);
        }

        [Test]
        public void GearChangesBattleDamage()
        {
            int Hit(ItemData armor)
            {
                var art = Art();
                var a = new Combatant("가", true, new Stats { Light = 10 }, new[] { art });
                var t = new Combatant("나", false, new Stats { Con = 10 }, new[] { art });
                if (armor != null) t.Gear = new[] { armor };
                var b = new Battle(new[] { a }, new[] { t }, new System.Random(1)) { DamageSpread = 0 };
                b.BeginTurn();
                int hp = t.Hp;
                b.UseArt(art, t);
                return hp - t.Hp;
            }
            Assert.AreEqual(Hit(null) / 2, Hit(Make(ItemKind.Clothes, i => i.defense = 50)), 1);
        }

        [Test]
        public void GearResistsHalveStatusChance()
        {
            var c = new Combatant("가", true, new Stats(), new[] { Art() }) { Gear = new[] { Make(ItemKind.Trinket, i => i.resists = new[] { StatusKind.Poison }) } };
            Assert.IsTrue(c.Resists(StatusKind.Poison));
            Assert.IsFalse(c.Resists(StatusKind.Bleed));
        }

        [Test]
        public void PotionUsesTheTurnAndLeavesTheBag()
        {
            var potion = Make(ItemKind.Consumable, i => i.heal = 50);
            var art = Art();
            var hero = new Combatant("가", true, new Stats { Con = 5, Light = 10 }, new[] { art });
            var bag = new Inventory();
            bag.Add(potion);
            var b = new Battle(new[] { hero }, new[] { new Combatant("나", false, new Stats { Con = 5 }, new[] { art }) }) { Bag = bag };
            b.BeginTurn();
            Assert.IsFalse(b.CanUseItem(potion, hero), "체력이 가득하면 쓸 수 없다");
            Assert.IsEmpty(b.UsableItems);

            hero.Hp = 10;
            Assert.IsTrue(b.UseItem(potion, hero));
            Assert.AreEqual(10 + hero.MaxHp / 2, hero.Hp);
            Assert.AreEqual(0, bag.Count(potion));
            Assert.IsNull(b.Current, "턴을 쓴다");
            Assert.IsTrue(b.Events.Any(e => e.Kind == EventKind.Heal));
        }

        [Test]
        public void PotionCuresInnerInjuryInAndOutOfBattle()
        {
            var pill = Make(ItemKind.Consumable, i => i.cures = new[] { StatusKind.InnerInjury });
            var h = Hero();
            h.Bag.Add(pill, 2);
            Assert.IsFalse(h.CanUse(pill), "멀쩡하면 쓸 수 없다");
            h.InnerInjured = true;
            Assert.IsTrue(h.Use(pill));
            Assert.IsFalse(h.InnerInjured);

            h.InnerInjured = true;
            var c = h.ToCombatant();
            var b = new Battle(new[] { c }, new[] { new Combatant("나", false, new Stats(), new[] { Art() }) }) { Bag = h.Bag };
            b.BeginTurn();
            Assert.IsTrue(b.UseItem(pill, c));
            Assert.IsFalse(c.InnerInjured);
        }

        [Test]
        public void TonicEasesFatigueAndRaisesInnerPower()
        {
            var tonic = Make(ItemKind.Consumable, i => { i.fatigue = 40; i.innerPower = 30; });
            var h = Hero();
            h.Fatigue = 25;
            h.Bag.Add(tonic);
            Assert.IsTrue(h.Use(tonic));
            Assert.AreEqual(0, h.Fatigue);
            Assert.AreEqual(30, h.InnerPower);
        }

        [Test]
        public void ElixirRaisesStatsForGoodPastTheRealmCap()
        {
            var elixir = Make(ItemKind.Elixir, i => i.stats.Con = 1);
            var h = new Character("나", new Stats { Con = 10 });
            h.Bag.Add(elixir);
            Assert.IsTrue(h.CanUse(elixir));
            Assert.IsTrue(h.Use(elixir));
            Assert.AreEqual(11, h.Stats.Con);
            Assert.AreEqual(0, h.Bag.Count(elixir));
            Assert.IsFalse(h.CanUse(elixir), "다 먹었다");
        }

        [Test]
        public void ElixirBreaksThroughWithoutInnerPowerOrClosedDoorTraining()
        {
            var elixir = Make(ItemKind.Elixir, i => i.breakthrough = true);
            var h = Hero();
            h.InnerPower = 30;
            h.Bag.Add(elixir, 2);
            Assert.IsFalse(h.CanAttemptBreakthrough, "폐관수련은 내공이 모자라 못 한다");
            Assert.IsTrue(h.Use(elixir));
            Assert.AreEqual(Realm.Iryu, h.Realm);
            Assert.AreEqual(0, h.InnerPower);
        }

        [Test]
        public void ElixirCannotPassAWallWithoutEnlightenment()
        {
            var elixir = Make(ItemKind.Elixir, i => i.breakthrough = true);
            var h = Hero();
            h.Realm = Realm.Ilryu;
            h.Bag.Add(elixir);
            Assert.IsFalse(h.CanUse(elixir));
            Assert.IsFalse(h.Use(elixir));
            Assert.AreEqual(1, h.Bag.Count(elixir), "못 먹으면 남는다");

            h.Enlightened = true;
            Assert.IsTrue(h.Use(elixir));
            Assert.AreEqual(Realm.Jeoljeong, h.Realm);
            Assert.IsFalse(h.Enlightened);

            h.Realm = Realm.Hwagyeong;
            h.Bag.Add(elixir);
            Assert.IsFalse(h.CanUse(elixir), "끝 경지");
        }

        [Test]
        public void ElixirsAreNotBattleItemsOrGear()
        {
            var elixir = Make(ItemKind.Elixir, i => i.stats.Outer = 1);
            var h = Hero();
            h.Bag.Add(elixir);
            Assert.IsFalse(h.Equip(elixir));
            var c = h.ToCombatant();
            var b = new Battle(new[] { c }, new[] { new Combatant("나", false, new Stats(), new[] { Art() }) }) { Bag = h.Bag };
            b.BeginTurn();
            Assert.IsEmpty(b.UsableItems);
        }

        [Test]
        public void GearDoesNotWorkAsAPotion()
        {
            var ring = Make(ItemKind.Trinket, i => i.stats.Outer = 1);
            var h = Hero();
            h.Bag.Add(ring);
            Assert.IsFalse(h.CanUse(ring));
        }

        [Test]
        public void ShopPricesFollowVirtue()
        {
            var sword = Make(ItemKind.Weapon, i => i.price = 100);
            var h = Hero();
            h.Money = 95;
            Assert.AreEqual(90, h.BuyPrice(sword), "인덕 5면 10% 싸다");
            Assert.IsTrue(h.Buy(sword));
            Assert.AreEqual(5, h.Money);
            Assert.IsFalse(h.Buy(sword), "돈이 모자라다");
            Assert.IsTrue(h.Sell(sword));
            Assert.AreEqual(55, h.Money);

            h.Stats.Virtue = 50;
            Assert.AreEqual(70, h.BuyPrice(sword), "최대 30%");
        }

        [Test]
        public void GiftsCannotBeSold()
        {
            var gift = Make(ItemKind.Weapon);
            var h = Hero();
            h.Bag.Add(gift);
            Assert.IsFalse(h.Sell(gift));
            Assert.AreEqual(1, h.Bag.Count(gift));
        }

        [Test]
        public void LuckRaisesDropChance()
        {
            var battle = ScriptableObject.CreateInstance<BattleData>();
            battle.drops.Add(new Drop { item = Make(ItemKind.Consumable), chance = 0 });
            var rng = new System.Random(1);
            Assert.IsEmpty(battle.RollLoot(rng, 0));
            Assert.IsNotEmpty(battle.RollLoot(rng, 100));
        }

        [Test]
        public void SaveKeepsBagAndEquipment()
        {
            var sword = ItemData.Find("정강검");
            var potion = ItemData.Find("금창약");
            var state = new GameState(Hero());
            state.Hero.Bag.Add(sword);
            state.Hero.Bag.Add(potion, 3);
            state.Hero.Equip(sword);

            var loaded = new GameState(new Character("?", new Stats()));
            SaveGame.Read(SaveGame.Write(state, "{}"), loaded, _ => null);
            Assert.AreSame(sword, loaded.Hero.Equipped[0]);
            Assert.IsNull(loaded.Hero.Equipped[1]);
            Assert.AreEqual(3, loaded.Hero.Bag.Count(potion));
            Assert.AreEqual(0, loaded.Hero.Bag.Count(sword));
        }

        [Test]
        public void ItemDataIsWiredUp()
        {
            Assert.IsNotEmpty(ItemData.All.Where(i => i.shop == "백로진"));
            foreach (var i in ItemData.All)
                if (i.shop != "") Assert.Greater(i.price, 0, i.name);

            var script = string.Concat(System.IO.Directory.GetFiles("Assets/_Project/Dialogue", "*.ink").Select(System.IO.File.ReadAllText));
            var gifts = Regex.Matches(script, "give\\(\"(.+?)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.IsNotEmpty(gifts);
            foreach (var name in gifts) Assert.IsNotNull(ItemData.Find(name), name);
        }
    }
}
