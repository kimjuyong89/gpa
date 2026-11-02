using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 상점. 왼쪽은 파는 물건, 오른쪽은 내 가방. 입고 있는 장비는 벗어야 팔 수 있다.
    public class ShopScreen
    {
        public readonly VisualElement Root = Ui.Box("fill", "dim");
        readonly Character hero;
        readonly GameState sect; // 있으면 문파 비고다. 공헌도로 바꾸고 팔 수는 없다
        readonly string shopId;
        readonly VisualElement panel = Ui.Box("menu", "panel", "shop-panel");
        readonly Action onClose;

        public ShopScreen(Character hero, string shopId, GameState sect, Action onClose)
        {
            this.hero = hero;
            this.sect = sect;
            this.shopId = shopId;
            this.onClose = onClose;
            Root.Add(panel);
            Refresh();
        }

        void Refresh()
        {
            panel.Clear();
            if (sect != null)
            {
                DrawVault();
                return;
            }
            var top = Ui.Box("menu-tabs");
            top.Add(Ui.Text($"{shopId} 장터   돈 {hero.Money}냥", "heading"));
            top.Add(Ui.Box("grow"));
            top.Add(Ui.Btn("닫기", onClose, true, "btn-small"));
            panel.Add(top);

            var split = Ui.Box("shop");
            var buy = new ScrollView();
            var sell = new ScrollView();
            foreach (var item in ItemData.All.Where(i => i.shop == shopId).OrderBy(i => i.kind).ThenBy(i => i.price))
            {
                var row = Row(item, 0);
                int price = hero.BuyPrice(item);
                row.Add(Ui.Btn($"{price}냥", () =>
                {
                    hero.Buy(item);
                    Sound.Play("Coin");
                    Refresh();
                }, hero.Money >= price, "item-btn"));
                buy.Add(row);
            }
            foreach (var item in hero.Bag.Items.ToList())
            {
                var row = Row(item, hero.Bag.Count(item));
                row.Add(Ui.Btn(item.price > 0 ? $"{Character.SellPrice(item)}냥" : "못 판다", () =>
                {
                    hero.Sell(item);
                    Sound.Play("Coin");
                    Refresh();
                }, item.price > 0, "item-btn"));
                sell.Add(row);
            }
            split.Add(Side("사기", buy));
            split.Add(Side("팔기", sell));
            panel.Add(split);
        }

        void DrawVault()
        {
            var top = Ui.Box("menu-tabs");
            top.Add(Ui.Text($"{shopId} 비고   공헌 {sect.Contribution}", "heading"));
            top.Add(Ui.Box("grow"));
            top.Add(Ui.Btn("닫기", onClose));
            panel.Add(top);
            var list = new ScrollView();
            foreach (var item in ItemData.All.Where(i => i.shop == shopId).OrderBy(i => i.contribution))
            {
                var row = Row(item, 0);
                int price = sect.VaultPrice(item);
                row.Add(Ui.Btn($"공헌 {price}", () =>
                {
                    sect.BuyFromVault(item);
                    Sound.Play("Coin");
                    Refresh();
                }, sect.Contribution >= price, "item-btn"));
                list.Add(row);
            }
            panel.Add(Side("바꿀 수 있는 것", list));
        }

        static VisualElement Side(string title, ScrollView list)
        {
            var side = Ui.Box("shop-side");
            side.Add(Ui.Text(title, "heading"));
            list.AddToClassList("item-list");
            side.Add(list);
            return side;
        }

        // 이름과 효과 한 줄. 뒤에 버튼을 붙여 쓴다. 메뉴의 소지품 탭도 쓴다.
        public static VisualElement Row(ItemData item, int count)
        {
            var row = Ui.Box("item-row");
            row.Add(Ui.ItemIcon(item));
            var text = Ui.Box("item-text");
            text.Add(Ui.Text(count > 1 ? $"{item.name} ×{count}" : item.name, "item-name"));
            text.Add(Ui.Text(Describe(item), "small", "muted", "wrap"));
            row.Add(text);
            return row;
        }

        public static string Describe(ItemData i)
        {
            var parts = new List<string>();
            if (i.manual != null) parts.Add($"{GradeName(i.manual.grade)} {Combatant.SkillLabel(i.manual.skill)} 무공, 오성 {Character.WisdomFor(i.manual)}");
            if (i.weapon != WeaponType.None) parts.Add(WeaponName(i.weapon));
            if (i.attack > 0) parts.Add($"공격 +{i.attack}%");
            if (i.defense > 0) parts.Add($"방어 +{i.defense}%");
            parts.AddRange(CharacterCreation.All.Where(k => i.stats.Ref(k) != 0).Select(k => $"{CreateScreen.StatNames[(int)k]} {(i.kind == ItemKind.Elixir ? "영구 " : "")}+{i.stats.Ref(k)}"));
            parts.AddRange(i.resists.Select(k => $"{Combatant.Label(k)} 저항"));
            if (i.heal > 0) parts.Add($"체력 {i.heal}%");
            if (i.restoreInner > 0) parts.Add($"내력 {i.restoreInner}%");
            parts.AddRange(i.cures.Select(k => $"{Combatant.Label(k)} 치료"));
            if (i.fatigue > 0) parts.Add($"피로 -{i.fatigue}");
            if (i.innerPower > 0) parts.Add($"내공 +{i.innerPower}");
            if (i.breakthrough) parts.Add("즉시 경지 돌파");
            return string.Join(", ", parts);
        }

        static string GradeName(ArtGrade g) => g == ArtGrade.Supreme ? "절세" : MenuScreen.RealmNames[(int)g];

        static string WeaponName(WeaponType w) => w switch
        {
            WeaponType.Sword => "검",
            WeaponType.Dagger => "단검",
            WeaponType.Blade => "도",
            _ => "권",
        };
    }
}
