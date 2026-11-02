using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 문파 안의 삶: 고유 규칙, 신분, 문규, 서열, 비고.
    public class SectLifeTests
    {
        static GameState Joined(Lineage forbidden = Lineage.Deviant)
        {
            var sect = ScriptableObject.CreateInstance<SectData>();
            sect.id = "시험문";
            sect.alignment = "정파";
            sect.master = "사부";
            sect.discipleAffinity = 10;
            sect.forbidden = forbidden;
            var s = new GameState(new Character("나", new Stats()), new[] { sect });
            s.Join("시험문");
            return s;
        }

        static MartialArt Art(Lineage line = Lineage.Common)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Outer;
            a.power = 100;
            a.lineage = line;
            return a;
        }

        [Test]
        public void NewMembersStartAsOuterDisciples()
        {
            var s = Joined();
            Assert.AreEqual(SectRank.Outer, s.Rank);
            Assert.AreEqual("외문제자", s.RankName);
        }

        [Test]
        public void RanksClimbInOrder()
        {
            var s = Joined();
            Assert.IsFalse(s.Promote(), "공헌이 모자라다");
            StringAssert.Contains("공헌", s.PromotionBlocker());

            s.Add("공헌.시험문", GameState.InnerContribution);
            Assert.IsTrue(s.Promote());
            Assert.AreEqual(SectRank.Inner, s.Rank);

            Assert.IsFalse(s.Promote(), "사부 호감이 모자라다");
            s.Set("호감.사부", 10);
            Assert.IsTrue(s.Promote());
            Assert.AreEqual(SectRank.Disciple, s.Rank);
            Assert.IsTrue(s.Disciple, "직전제자 기능이 이어진다");

            Assert.IsFalse(s.Promote(), "일류가 아니다");
            s.Hero.Realm = Realm.Ilryu;
            StringAssert.Contains("공헌", s.PromotionBlocker());
            s.Add("공헌.시험문", GameState.ElderContribution);
            Assert.IsTrue(s.Promote());
            Assert.AreEqual(SectRank.Elder, s.Rank);
            Assert.AreEqual("끝", s.PromotionBlocker());
            Assert.IsFalse(s.Promote());
        }

        [Test]
        public void OldSavesWithTheDiscipleFlagKeepTheirRank()
        {
            var s = Joined();
            s.Disciple = true;
            Assert.AreEqual(SectRank.Disciple, s.Rank);
        }

        [Test]
        public void ForbiddenArtsRaiseSuspicionUntilExpelled()
        {
            var s = Joined();
            Assert.IsFalse(s.BreakRule(new[] { Art(Lineage.Orthodox) }), "허용된 무공");
            Assert.AreEqual(0, s.Get("발각.시험문"));

            var forbidden = new[] { Art(Lineage.Deviant) };
            for (int i = 0; i < 6; i++) Assert.IsFalse(s.BreakRule(forbidden));
            Assert.AreEqual(90, s.Get("발각.시험문"));
            Assert.IsTrue(s.BreakRule(forbidden), "들켰다");
            Assert.IsNull(s.Sect);
        }

        [Test]
        public void SectsWithoutARuleForgiveEverything()
        {
            var s = Joined(Lineage.Common);
            Assert.IsFalse(s.BreakRule(new[] { Art(Lineage.Deviant) }));
        }

        [Test]
        public void RankingRisesAndPaysContribution()
        {
            var s = Joined();
            Assert.AreEqual(10, s.Ranking);
            s.WinRanking();
            Assert.AreEqual(9, s.Ranking);
            Assert.AreEqual(5, s.Contribution);
            s.WinRanking();
            Assert.AreEqual(11, s.Contribution, "오를수록 보상이 크다");
            for (int i = 0; i < 20; i++) s.WinRanking();
            Assert.AreEqual(1, s.Ranking, "1위가 끝이다");
        }

        [Test]
        public void VaultSpendsContributionAndElderPaysLess()
        {
            var s = Joined();
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.shop = "시험문";
            item.contribution = 100;
            Assert.IsFalse(s.BuyFromVault(item));
            s.Add("공헌.시험문", 150);
            Assert.IsTrue(s.BuyFromVault(item));
            Assert.AreEqual(1, s.Hero.Bag.Count(item));
            Assert.AreEqual(50, s.Contribution);

            s.Set("신분.시험문", (int)SectRank.Elder);
            Assert.AreEqual(70, s.VaultPrice(item));
        }

        [Test]
        public void OtherSectsVaultIsClosed()
        {
            var s = Joined();
            s.Add("공헌.시험문", 999);
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.shop = "다른문";
            Assert.IsFalse(s.BuyFromVault(item));
        }

        static Combatant Hero(MartialArt art, SectTrait trait) =>
            new("가", true, new Stats { Light = 20, Con = 40 }, new[] { art }) { Trait = trait };

        [Test]
        public void UnbrokenKeepsTheComboThroughOtherActions()
        {
            var art = ScriptableObject.CreateInstance<MartialArt>();
            art.kind = ArtKind.Outer;
            art.moves = new[] { new Move { name = "일", power = 5 }, new Move { name = "이", power = 5 } };
            var mastery = new System.Collections.Generic.Dictionary<MartialArt, int> { [art] = 10 };
            var hero = new Combatant("가", true, new Stats { Light = 20, Con = 40 }, new[] { art }, Row.Front, false, 0, mastery) { Trait = SectTrait.Unbroken };
            var foe = new Combatant("적", false, new Stats { Con = 40 }, new[] { Art() });
            var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1));
            b.BeginTurn();
            b.UseMove(art, art.Moves[0], foe);
            b.BeginTurn();
            b.Meditate();
            b.BeginTurn();
            Assert.IsTrue(hero.ComboNext(art, art.Moves[1]), "운기조식으로 끊기지 않는다");
        }

        [Test]
        public void PredatorHitsAfflictedFoesHarder()
        {
            int Hit(bool afflicted)
            {
                var art = Art();
                art.inflicts = StatusKind.None;
                var hero = Hero(art, SectTrait.Predator);
                var foe = new Combatant("적", false, new Stats { Con = 50 }, new[] { Art() });
                var b = new Battle(new[] { hero }, new[] { foe }, new System.Random(1)) { DamageSpread = 0 };
                if (afflicted) foe.Statuses.Add(new Status { Kind = StatusKind.Bleed, Turns = 3 });
                b.BeginTurn();
                int hp = foe.Hp;
                b.UseArt(art, foe);
                return hp - foe.Hp;
            }
            Assert.AreEqual(1.25f, Hit(true) / (float)Hit(false), 0.03f);
        }

        [Test]
        public void SectDataIsWiredUp()
        {
            SectData Sect(string file) => AssetDatabase.LoadAssetAtPath<SectData>($"Assets/_Project/Data/Sects/{file}.asset");
            Assert.AreEqual(SectTrait.Unbroken, Sect("Cheongun").trait);
            Assert.AreEqual(SectTrait.Predator, Sect("Heuksa").trait);
            Assert.AreEqual(Lineage.Deviant, Sect("Cheongun").forbidden);
            Assert.AreEqual(Lineage.Orthodox, Sect("Heuksa").forbidden);
            foreach (var id in new[] { "청운검문", "흑사방" })
            {
                var stock = ItemData.All.Where(i => i.shop == id).ToList();
                Assert.GreaterOrEqual(stock.Count, 3, id);
                foreach (var item in stock) Assert.Greater(item.contribution, 0, item.name);
            }
        }
    }
}
