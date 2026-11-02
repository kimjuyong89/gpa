using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Narrative;
using UnityEngine;

namespace GPA.World
{
    public enum NodeKind { Town, Outdoor, Locked } // 마을, 야외, 아직 갈 수 없는 곳

    [Serializable]
    public class WorldNode
    {
        public string id;
        public NodeKind kind;
        [Range(0, 1)] public float encounterChance; // 한 걸음마다 조우할 확률
        public string[] encounters;  // 조우 전투 id (BattleData.id)
        public RegionData region;    // 걸어 다니는 지도. 잠긴 곳과 조우 지대에는 없다
    }

    [CreateAssetMenu(menuName = "GPA/월드맵")]
    public class WorldMapData : ScriptableObject
    {
        const int StepsPerFatigue = 4, StepsPerFatigueAgile = 6; // 이만큼 걸으면 피로 +1. 경공을 익히면 덜 지친다

        public string start;     // 새 게임을 시작하는 곳
        public string overworld; // 마을 밖 큰 지도의 노드
        public List<WorldNode> nodes = new();

        public WorldNode Node(string id) => nodes.FirstOrDefault(n => n.id == id);

        // 큰 지도에서 한 걸음. 피로가 쌓이고, 조우 지대면 전투 id를 돌려준다.
        public string Walk(GameState s, TileDef tile, System.Random rng)
        {
            s.Add("걸음", 1);
            int every = s.Hero.MovementArt != null ? StepsPerFatigueAgile : StepsPerFatigue;
            if (s.Get("걸음") % every == 0) s.Hero.Fatigue = Math.Min(s.Hero.MaxFatigue, s.Hero.Fatigue + 1);

            var zone = tile == null || tile.encounterNode == "" ? null : Node(tile.encounterNode);
            if (zone == null || zone.encounters.Length == 0 || rng.NextDouble() >= zone.encounterChance) return null;
            return zone.encounters[rng.Next(zone.encounters.Length)];
        }
    }
}
