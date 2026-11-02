using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPA.World
{
    // 지도 글자 하나가 무엇인지. 크기가 1칸보다 크면 소품(건물·나무)이고, 글자 위치가 왼쪽 위다.
    [Serializable]
    public class TileDef
    {
        public string key;          // 글자 한 개
        public Texture2D sheet;
        public Vector2Int cell;     // 시트에서 왼쪽 위 칸 (16px 단위, 위에서부터)
        public Vector2Int size = Vector2Int.one;
        public bool solid;
        public string ground = "."; // 소품 아래 깔 바닥 글자
        public bool overlay;        // 1칸이어도 바닥 위에 겹쳐 그린다 (수풀 등)
        public string portal = "";  // 밟으면 이 노드로 들어간다
        public string encounterNode = ""; // 밟으면 이 노드의 조우를 굴린다
    }

    [Serializable]
    public class NpcDef
    {
        public string key;      // 지도에 찍힌 글자
        public string name;
        public Texture2D sheet; // Ninja Adventure 캐릭터 시트 (4방향 × 걷기 4프레임)
        public string knot;     // 말을 걸면 시작할 대화
        public Texture2D prop;  // 있으면 사람 대신 움직이지 않는 물체로 그린다 (나물, 그루터기 등)
        public Vector2Int propCell;
    }

    // 걸어 다니는 지역맵. layout은 한 줄이 한 행, 한 글자가 한 칸.
    [CreateAssetMenu(menuName = "GPA/지역맵")]
    public class RegionData : ScriptableObject
    {
        [TextArea(10, 40)] public string layout;
        public List<TileDef> tiles = new();
        public List<NpcDef> npcs = new();
        public string start = "@";
        public string exit = "E";       // 밟으면 월드맵으로 나간다
        public string specialGround = "="; // 시작·출구·NPC 칸 아래 바닥

        public string[] Rows => layout.Replace("\r", "").Trim('\n').Split('\n');
        public TileDef Tile(char c) => tiles.Find(t => t.key.Length > 0 && t.key[0] == c);
        public NpcDef Npc(char c) => npcs.Find(n => n.key.Length > 0 && n.key[0] == c);
    }
}
