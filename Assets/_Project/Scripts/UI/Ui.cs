using System;
using System.Collections.Generic;
using GPA.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // UI Toolkit 요소를 코드로 만드는 작은 도우미와 그림 불러오기.
    public static class Ui
    {
        public static VisualElement Box(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, bool enabled = true, params string[] classes)
        {
            var b = new Button(() =>
            {
                Sound.Play("Accept");
                onClick();
            }) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            b.SetEnabled(enabled);
            return b;
        }

        // 메뉴 상단 탭: 글자와 주묵 밑줄.
        public static Button Tab(string text, bool selected, Action onClick)
        {
            var b = new Button(() =>
            {
                Sound.Play("Move1");
                onClick();
            }) { text = text };
            b.AddToClassList("tab");
            b.EnableInClassList("tab-selected", selected);
            return b;
        }

        // 아이템 종류별 아이콘 이름(Art/Resources/Icons). 없으면 null이고 한자를 보여 준다.
        static string IconOf(ItemData i) => i.name switch
        {
            "지혈 부적" or "해독 향낭" => "talisman",
            "행운 엽전" => "coin",
            _ => i.kind switch
            {
                ItemKind.Weapon => i.weapon == WeaponType.Dagger ? "dagger" : "jian",
                ItemKind.Clothes => "robe",
                ItemKind.Gloves => "glove",
                ItemKind.Consumable or ItemKind.Elixir => "pill",
                _ => null,
            },
        };

        static string GlyphOf(ItemKind k) => k switch
        {
            ItemKind.Boots => "履",
            ItemKind.Trinket => "珮",
            ItemKind.Elixir => "丹",
            ItemKind.Manual => "書",
            _ => "物",
        };

        // 등급 프레임 안에 든 아이템 아이콘. 문파 지급품(값 0인 장비)은 옥, 영약은 금룡 프레임.
        public static VisualElement ItemIcon(ItemData item)
        {
            var box = Box("item-icon-box");
            if (item.kind == ItemKind.Elixir) box.AddToClassList("frame-t5");
            else if (item.IsGear && item.price == 0) box.AddToClassList("frame-t3");
            var icon = IconOf(item);
            var t = icon == null ? null : Tex("Icons/" + icon);
            if (t != null) box.Add(Image(Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.zero, 16), "item-icon"));
            else box.Add(Text(GlyphOf(item.kind), "item-glyph"));
            return box;
        }

        // 막대 하나: 바깥과 채우는 부분. SetBar로 비율을 바꾼다.
        public static (VisualElement bar, VisualElement fill) Bar(string fillClass)
        {
            var bar = Box("bar");
            var fill = Box("bar-fill", fillClass);
            bar.Add(fill);
            return (bar, fill);
        }

        public static void SetBar(VisualElement fill, float ratio) =>
            fill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100);

        static readonly Dictionary<string, Texture2D> textures = new();

        static Texture2D Tex(string path)
        {
            if (!textures.TryGetValue(path, out var t) || t == null) textures[path] = t = Resources.Load<Texture2D>(path);
            return t;
        }

        public static Texture2D Sheet(string who) => Tex($"NinjaAdventure/Characters/{who}/SpriteSheet");

        public static Sprite Face(string who)
        {
            var t = Tex($"NinjaAdventure/Characters/{who}/Faceset");
            return t == null ? null : Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.zero, 16);
        }

        // 초상화: Art/Resources/Portraits/<이름>.png가 있으면 그것을, 없으면 픽셀 얼굴을 쓴다.
        public static Sprite Portrait(string name, string fallbackSheet)
        {
            var t = Tex("Portraits/" + name);
            return t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.zero, 100) : Face(fallbackSheet);
        }

        // 전투 입상. 없으면 null.
        public static Sprite Body(string name)
        {
            var t = Tex("Bodies/" + name);
            return t == null ? null : Sprite.Create(t, new Rect(0, 0, t.width, t.height), Vector2.zero, 100);
        }

        // 주인공 초상화 이름. 얼굴 번호마다 하나다.
        public static string HeroPortrait(int look) => $"주인공{look + 1}";

        // 시트의 한 칸. 줄은 위에서부터 센다.
        public static Sprite Frame(Texture2D sheet, int col, int row, int w = 16, int h = 16) =>
            Sprite.Create(sheet, new Rect(col * w, sheet.height - (row + 1) * h, w, h), Vector2.zero, 16);

        public static Sprite[] Strip(string path, int count)
        {
            var t = Tex(path);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++) frames[i] = Frame(t, i, 0, t.width / count, t.height);
            return frames;
        }

        public static VisualElement Image(Sprite s, params string[] classes)
        {
            var e = Box(classes);
            if (s != null) e.style.backgroundImage = new StyleBackground(s);
            return e;
        }
    }
}
