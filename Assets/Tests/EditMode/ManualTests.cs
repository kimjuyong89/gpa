using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    // 비급: 소지품에서 읽으면 무공을 익힌다.
    public class ManualTests
    {
        static ItemData Manual(ArtGrade grade = ArtGrade.Third, ArtKind kind = ArtKind.Outer, InnerNature nature = InnerNature.None)
        {
            var art = ScriptableObject.CreateInstance<MartialArt>();
            art.kind = kind;
            art.grade = grade;
            art.nature = nature;
            var i = ScriptableObject.CreateInstance<ItemData>();
            i.kind = ItemKind.Manual;
            i.manual = art;
            return i;
        }

        static Character Hero(int wis = 5) => new("나", new Stats { Wis = wis, Con = 5 });

        [Test]
        public void ReadingTeachesTheArtAndUsesTheBook()
        {
            var book = Manual();
            var h = Hero();
            h.Bag.Add(book);
            Assert.IsTrue(h.CanRead(book));
            Assert.IsTrue(h.Read(book));
            Assert.IsTrue(h.Arts.Contains(book.manual));
            Assert.AreEqual(0, h.Bag.Count(book));
            Assert.AreEqual(Character.ReadFatigue, h.Fatigue);
        }

        [Test]
        public void KnownArtsCannotBeReadAgain()
        {
            var book = Manual();
            var h = Hero();
            h.Bag.Add(book, 2);
            h.Read(book);
            Assert.IsFalse(h.CanRead(book));
            Assert.AreEqual(1, h.Bag.Count(book), "남은 책은 그대로다");
        }

        [Test]
        public void TiredHeroesCannotRead()
        {
            var book = Manual();
            var h = Hero();
            h.Bag.Add(book);
            h.Fatigue = h.MaxFatigue - 5;
            Assert.IsFalse(h.CanRead(book));
        }

        [TestCase(ArtGrade.Third, 0, true)]
        [TestCase(ArtGrade.Second, 7, false)]
        [TestCase(ArtGrade.Second, 8, true)]
        [TestCase(ArtGrade.First, 13, false)]
        [TestCase(ArtGrade.First, 14, true)]
        [TestCase(ArtGrade.Peak, 19, false)]
        [TestCase(ArtGrade.Peak, 20, true)]
        [TestCase(ArtGrade.Supreme, 25, false)]
        [TestCase(ArtGrade.Supreme, 26, true)]
        public void HigherGradesNeedMoreWisdom(ArtGrade grade, int wis, bool can)
        {
            var book = Manual(grade);
            var h = Hero(wis);
            h.Bag.Add(book);
            Assert.AreEqual(can, h.CanRead(book));
        }

        [Test]
        public void GearStatsCountTowardsWisdom()
        {
            var book = Manual(ArtGrade.Second);
            var h = Hero(6);
            var charm = ScriptableObject.CreateInstance<ItemData>();
            charm.kind = ItemKind.Trinket;
            charm.stats.Wis = 2;
            h.Bag.Add(charm);
            h.Equip(charm);
            h.Bag.Add(book);
            Assert.IsTrue(h.CanRead(book));
        }

        [Test]
        public void MindBooksFollowTheInnerRoute()
        {
            var yang = ScriptableObject.CreateInstance<MartialArt>();
            yang.kind = ArtKind.Mind;
            yang.nature = InnerNature.Yang;
            var book = Manual(kind: ArtKind.Mind, nature: InnerNature.Yin);
            var h = new Character("나", new Stats { Con = 5 }, new[] { yang });
            h.Bag.Add(book);
            Assert.IsFalse(h.CanRead(book), "양을 운용하면 음 심법을 읽지 못한다");
        }

        [Test]
        public void BooksAreNotGearOrPotions()
        {
            var book = Manual();
            var h = Hero();
            h.Bag.Add(book);
            Assert.IsFalse(h.Equip(book));
            Assert.IsFalse(h.CanUse(book));
        }

        [Test]
        public void EveryFamilyArtHasABookInTheShop()
        {
            var books = ItemData.All.Where(i => i.kind == ItemKind.Manual).ToList();
            foreach (var name in new[] { "오호단문도", "철사장", "맹호권", "선풍각" })
            {
                var book = books.FirstOrDefault(i => i.manual != null && i.manual.displayName == name);
                Assert.IsNotNull(book, name);
                Assert.AreEqual("백로진", book.shop);
                Assert.Greater(book.price, 0);
            }
        }
    }
}
