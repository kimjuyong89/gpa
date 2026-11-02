using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using GPA.World;
using NUnit.Framework;
using UnityEditor;

namespace GPA.Tests
{
    // 실제 데이터 에셋끼리 이름이 맞는지 확인한다: 지도·장소가 부르는 대화와 전투가 실제로 있는가.
    public class ContentTests
    {
        static WorldMapData World => AssetDatabase.LoadAssetAtPath<WorldMapData>("Assets/_Project/Data/WorldMap.asset");

        [Test]
        public void EveryKnotOnTheMapExists()
        {
            var json = DialogueTests.CompileFile("Baekrojin.ink");
            var knots = World.nodes.Where(n => n.region != null).SelectMany(n => n.region.npcs.Select(p => p.knot));

            foreach (var knot in knots)
            {
                var d = new Dialogue(json, new GameState(new Character("나", new Stats())));
                Assert.DoesNotThrow(() => d.Start(knot), knot);
            }
        }

        [Test]
        public void EveryEncounterHasBattleData()
        {
            var battleIds = AssetDatabase.FindAssets("t:BattleData")
                .Select(g => AssetDatabase.LoadAssetAtPath<BattleData>(AssetDatabase.GUIDToAssetPath(g)).id)
                .ToList();
            foreach (var id in World.nodes.SelectMany(n => n.encounters))
                CollectionAssert.Contains(battleIds, id);
        }

        [Test]
        public void PortalsAndZonesPointToRealNodes()
        {
            Assert.IsNotNull(World.Node(World.start)?.region);
            Assert.IsNotNull(World.Node(World.overworld)?.region);
            foreach (var tile in World.nodes.Where(n => n.region != null).SelectMany(n => n.region.tiles))
            {
                if (tile.portal != "") Assert.IsNotNull(World.Node(tile.portal), tile.portal);
                if (tile.encounterNode != "") Assert.IsNotNull(World.Node(tile.encounterNode), tile.encounterNode);
            }
        }

        [Test]
        public void EveryFighterHasABattleFigure()
        {
            var names = AssetDatabase.FindAssets("t:BattleData")
                .Select(g => AssetDatabase.LoadAssetAtPath<BattleData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => !b.id.StartsWith("산_")) // 산짐승은 픽셀 그림으로 싸운다
                .SelectMany(b => b.enemies.Select(e => e.name))
                .Concat(new[] { "소육", "주인공1", "주인공2", "주인공3", "주인공4" });
            foreach (var name in names)
            {
                Assert.IsTrue(System.IO.File.Exists($"Assets/_Project/Art/Resources/Bodies/{name}.png"), name);
                Assert.IsTrue(System.IO.File.Exists($"Assets/_Project/Art/Resources/Portraits/{name}.png"), name);
            }
        }

        [Test]
        public void EveryNpcHasAPortrait()
        {
            var names = World.nodes.Where(n => n.region != null).Where(n => n.id != GPA.Narrative.GameState.MountainNode).SelectMany(n => n.region.npcs.Select(p => p.name))
                .Concat(new[] { "주인공1", "주인공2", "주인공3", "주인공4" });
            foreach (var name in names)
                Assert.IsTrue(System.IO.File.Exists($"Assets/_Project/Art/Resources/Portraits/{name}.png"), name);
        }

        [Test]
        public void RegionTilesAndNpcsAreDefined()
        {
            foreach (var region in World.nodes.Select(n => n.region).Where(r => r != null))
            foreach (char c in region.layout.Replace("\r", "").Replace("\n", ""))
                Assert.IsTrue(region.Tile(c) != null || region.Npc(c) != null || region.start[0] == c || region.exit[0] == c,
                    $"{region.name}: '{c}' 글자의 정의가 없다");
        }
    }
}
