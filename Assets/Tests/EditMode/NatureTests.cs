using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // 내공의 길: 운용 심법의 음양이 배우고 바꿀 수 있는 심법을 정한다. 전투에는 영향이 없다.
    public class NatureTests
    {
        static MartialArt Mind(InnerNature nature)
        {
            var a = ScriptableObject.CreateInstance<MartialArt>();
            a.kind = ArtKind.Mind;
            a.nature = nature;
            return a;
        }

        static Character Hero(params MartialArt[] arts) => new("나", new Stats(), arts);

        [Test]
        public void HarmonyLearnsEverything()
        {
            var h = Hero(Mind(InnerNature.Harmony));
            Assert.AreEqual(InnerNature.Harmony, h.Route);
            Assert.IsTrue(h.CanLearnMind(Mind(InnerNature.Yin)));
            Assert.IsTrue(h.CanLearnMind(Mind(InnerNature.Yang)));
        }

        [Test]
        public void YangLearnsOnlyYang()
        {
            var h = Hero(Mind(InnerNature.Yang));
            Assert.AreEqual(InnerNature.Yang, h.Route);
            Assert.IsTrue(h.CanLearnMind(Mind(InnerNature.Yang)));
            Assert.IsFalse(h.CanLearnMind(Mind(InnerNature.Yin)));
        }

        [Test]
        public void YinLearnsOnlyYin()
        {
            var h = Hero(Mind(InnerNature.Yin));
            Assert.IsTrue(h.CanLearnMind(Mind(InnerNature.Yin)));
            Assert.IsFalse(h.CanLearnMind(Mind(InnerNature.Yang)));
        }

        [Test]
        public void AnyRouteCanReturnToHarmony()
        {
            Assert.IsTrue(Hero(Mind(InnerNature.Yin)).CanLearnMind(Mind(InnerNature.Harmony)));
            Assert.IsTrue(Hero(Mind(InnerNature.Yang)).CanLearnMind(Mind(InnerNature.Harmony)));
        }

        [Test]
        public void RouteFollowsTheActiveMindSoHarmonyIsTheWayAcross()
        {
            MartialArt harmony = Mind(InnerNature.Harmony), yang = Mind(InnerNature.Yang), yin = Mind(InnerNature.Yin);
            var h = Hero(harmony, yang, yin);
            h.ActiveMind = yang;
            Assert.IsFalse(h.CanLearnMind(yin), "양에서 음으로 바로 바꿀 수 없다");
            Assert.IsTrue(h.CanLearnMind(harmony));

            h.ActiveMind = harmony;
            Assert.IsTrue(h.CanLearnMind(yin), "조화로 돌아오면 다시 열린다");
        }

        [Test]
        public void HeroWithoutAMindIsNotLimited()
        {
            Assert.IsTrue(Hero().CanLearnMind(Mind(InnerNature.Yin)));
        }

        [Test]
        public void OuterArtsAreNotLimited()
        {
            var sword = ScriptableObject.CreateInstance<MartialArt>();
            sword.kind = ArtKind.Outer;
            sword.nature = InnerNature.Yin;
            Assert.IsTrue(Hero(Mind(InnerNature.Yang)).CanLearnMind(sword));
        }

        [Test]
        public void SectLearningRespectsTheRoute()
        {
            var yin = Mind(InnerNature.Yin);
            yin.displayName = "음공";
            var sect = ScriptableObject.CreateInstance<SectData>();
            sect.id = "시험문";
            sect.alignment = "사파";
            sect.master = "사부";
            sect.arts = new[] { new SectArt { art = yin, contribution = 0 } };
            var s = new GameState(Hero(Mind(InnerNature.Yang)), new[] { sect });
            Assert.IsTrue(s.Join("시험문"), "입문은 된다");
            Assert.IsFalse(s.Knows("음공"), "그러나 심법은 받지 못한다");
            Assert.IsFalse(s.CanLearn("음공"));
            Assert.IsFalse(s.LearnSectArt("음공"));
        }

        [Test]
        public void NatureDoesNotChangeBattles()
        {
            var yin = Hero(Mind(InnerNature.Yin)).ToCombatant();
            var yang = Hero(Mind(InnerNature.Yang)).ToCombatant();
            Assert.AreEqual(yin.MaxHp, yang.MaxHp);
            Assert.AreEqual(yin.DamageTaken, yang.DamageTaken);
        }

        [Test]
        public void DataMatchesTheSetting()
        {
            MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"Assets/_Project/Data/MartialArts/{file}.asset");
            Assert.AreEqual(InnerNature.Harmony, Art("Jeonjinsim").nature);
            Assert.AreEqual(InnerNature.Yang, Art("Cheongsimgyeol").nature);
            Assert.AreEqual(InnerNature.Yin, Art("Eumsagong").nature);
            Assert.AreEqual(InnerNature.Harmony, Art("ManryuSim").nature);
        }
    }
}
