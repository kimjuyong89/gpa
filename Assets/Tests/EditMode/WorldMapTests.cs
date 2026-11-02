using System.Collections.Generic;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using GPA.World;
using NUnit.Framework;
using UnityEngine;

namespace GPA.Tests
{
    public class WorldMapTests
    {
        WorldMapData map;
        GameState state;
        readonly TileDef road = new(), reeds = new() { encounterNode = "수풀" };
        readonly System.Random rng = new(1);

        [SetUp]
        public void SetUp()
        {
            map = ScriptableObject.CreateInstance<WorldMapData>();
            map.overworld = "강호";
            map.nodes = new List<WorldNode>
            {
                new() { id = "강호", kind = NodeKind.Outdoor, encounters = new string[0] },
                new() { id = "수풀", kind = NodeKind.Outdoor, encounterChance = 1, encounters = new[] { "도적" } },
            };
            state = new GameState(new Character("나", new Stats())) { Location = "강호" };
        }

        [Test]
        public void WalkingTiresEveryFourSteps()
        {
            for (int i = 0; i < 12; i++) map.Walk(state, road, rng);
            Assert.AreEqual(3, state.Hero.Fatigue);
        }

        [Test]
        public void MovementArtEasesWalking()
        {
            var step = ScriptableObject.CreateInstance<MartialArt>();
            step.kind = ArtKind.Movement;
            state.Hero.Arts.Add(step);

            for (int i = 0; i < 12; i++) map.Walk(state, road, rng);

            Assert.AreEqual(2, state.Hero.Fatigue);
        }

        [Test]
        public void FatigueStopsAtMaximum()
        {
            state.Hero.Fatigue = state.Hero.MaxFatigue;
            for (int i = 0; i < 8; i++) map.Walk(state, road, rng);
            Assert.AreEqual(state.Hero.MaxFatigue, state.Hero.Fatigue);
        }

        [Test]
        public void OnlyEncounterZonesStartBattles()
        {
            Assert.IsNull(map.Walk(state, road, rng));
            Assert.IsNull(map.Walk(state, null, rng));
            Assert.AreEqual("도적", map.Walk(state, reeds, rng));
        }
    }
}
