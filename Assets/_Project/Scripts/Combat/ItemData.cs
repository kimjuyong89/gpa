using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GPA.Combat
{
    public enum ItemKind { Weapon, Clothes, Boots, Gloves, Trinket, Consumable, Elixir } // 앞 다섯의 순서가 장비 칸 번호다. 장신구는 4·5번 두 칸
    public enum WeaponType { None, Sword, Dagger, Fist } // 검, 단검, 권

    // 아이템 하나의 데이터. 에셋 파일 이름이 곧 게임에 보이는 이름이고 저장 키다.
    // Data/Resources/Items 아래에 두면 게임이 알아서 찾는다. 수치는 임시값 (docs/12_아이템.md).
    [CreateAssetMenu(menuName = "GPA/아이템")]
    public class ItemData : ScriptableObject
    {
        public ItemKind kind;
        [Tooltip("기본 값. 0이면 상점에 팔 수 없다")]
        public int price;
        [Tooltip("이 상점에서 판다. 비우면 팔지 않는다")]
        public string shop = "";

        [Header("장비")]
        public WeaponType weapon;
        [Tooltip("주는 피해 +%")]
        public int attack;
        [Tooltip("받는 피해 -%")]
        public int defense;
        [Tooltip("장비: 입는 동안. 영약: 먹으면 영구히")]
        public Stats stats = new();
        [Tooltip("걸릴 확률이 절반이 된다")]
        public StatusKind[] resists = Array.Empty<StatusKind>();

        [Header("소모품")]
        [Tooltip("최대 체력의 %")]
        public int heal;
        [Tooltip("최대 내력의 %")]
        public int restoreInner;
        public StatusKind[] cures = Array.Empty<StatusKind>();
        [Tooltip("전투 밖에서 줄이는 피로")]
        public int fatigue;
        [Tooltip("전투 밖에서 얻는 내공 축적량")]
        public int innerPower;

        [Header("영약")]
        [Tooltip("내공을 채우지 않아도, 폐관수련 없이 바로 다음 경지에 오른다. 큰 벽은 깨달음이 있어야 넘는다")]
        public bool breakthrough;

        public bool IsGear => kind < ItemKind.Consumable;

        public static ItemData Find(string name) => Resources.Load<ItemData>("Items/" + name);
        public static ItemData[] All => Resources.LoadAll<ItemData>("Items");
    }

    // 소지품: 아이템별 개수.
    public class Inventory
    {
        internal readonly Dictionary<ItemData, int> counts = new(); // 세이브용으로 internal

        public int Count(ItemData item) => counts.TryGetValue(item, out var n) ? n : 0;
        public void Add(ItemData item, int n = 1) => counts[item] = Count(item) + n;

        public bool Remove(ItemData item)
        {
            if (Count(item) == 0) return false;
            if (--counts[item] == 0) counts.Remove(item);
            return true;
        }

        public IEnumerable<ItemData> Items => counts.Keys.OrderBy(i => i.kind).ThenBy(i => i.price).ThenBy(i => i.name);
    }
}
