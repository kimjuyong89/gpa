using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;

namespace GPA.Tests
{
    public class CharacterCreationTests
    {
        static Stats Spent()
        {
            var s = CharacterCreation.NewStats();
            s.Outer += 5;
            s.Light += 5;
            s.Con += 5; // 15 포인트 모두 사용
            return s;
        }

        [Test]
        public void MustSpendAllPoints()
        {
            var s = CharacterCreation.NewStats();
            Assert.AreEqual(CharacterCreation.Points, CharacterCreation.Remaining(s));
            Assert.IsFalse(CharacterCreation.IsValid("유랑객", 0, s));
            Assert.IsTrue(CharacterCreation.IsValid("유랑객", 0, Spent()));
        }

        [Test]
        public void StatRangeIsEnforced()
        {
            var s = CharacterCreation.NewStats();
            Assert.IsFalse(CharacterCreation.CanLower(s, StatKind.Luck));
            s.Outer = CharacterCreation.Max;
            Assert.IsFalse(CharacterCreation.CanRaise(s, StatKind.Outer));
            Assert.IsTrue(CharacterCreation.CanRaise(s, StatKind.Light));

            var over = Spent();
            over.Outer = CharacterCreation.Max + 1;
            over.Light -= 1;
            Assert.IsFalse(CharacterCreation.IsValid("유랑객", 0, over));
        }

        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("  유랑객 ", true)]
        [TestCase("아홉글자의긴이름이다", false)]
        public void NameRules(string name, bool valid)
        {
            Assert.AreEqual(valid, CharacterCreation.IsValid(name, 0, Spent()));
        }

        [Test]
        public void AppearanceMustBeInList()
        {
            Assert.IsFalse(CharacterCreation.IsValid("유랑객", CharacterCreation.AppearanceCount, Spent()));
        }

        [Test]
        public void CreateTrimsNameAndKeepsChoices()
        {
            var hero = CharacterCreation.Create(" 유랑객 ", 2, Spent(), null);
            Assert.AreEqual("유랑객", hero.Name);
            Assert.AreEqual(2, hero.Appearance);
            Assert.AreEqual(8, hero.Stats.Outer);
        }
    }
}
