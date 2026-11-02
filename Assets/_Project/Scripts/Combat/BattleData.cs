using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GPA.Combat
{
    // 인스펙터에서 편집하는 전투 참가자 설정. 적과 동료에 쓴다.
    [Serializable]
    public class UnitSetup
    {
        public string name;
        public string sprite = "NinjaDark"; // Art/Resources/NinjaAdventure/Characters 아래 폴더
        public bool ally;
        public Row row;
        public int lane = 1;                // 가로 칸 0~2. 겹치면 전투가 옆 칸으로 옮긴다
        public Temper temper;
        public int realm;
        public Stats stats;
        public MartialArt[] arts;

        public Combatant Create(int? realmOverride = null) =>
            new(name, ally, stats, arts, row, realm: realmOverride ?? realm) { Sprite = sprite, Lane = lane, Temper = temper };
    }

    [Serializable]
    public class Drop
    {
        public ItemData item;
        [Range(0, 1)] public float chance;
    }

    // 대본의 "# battle: <id>" 태그가 부르는 전투.
    [CreateAssetMenu(menuName = "GPA/전투")]
    public class BattleData : ScriptableObject
    {
        public string id;
        public int reward; // 이기면 받는 돈
        public bool solo;  // 1:1 대련: 동료가 끼지 않는다
        public List<UnitSetup> enemies = new();
        public List<Drop> drops = new();

        // 전리품. 기연 1당 확률 +1%p.
        public List<ItemData> RollLoot(System.Random rng, int luck) =>
            drops.Where(d => rng.NextDouble() < d.chance + luck * 0.01f).Select(d => d.item).ToList();
    }
}
