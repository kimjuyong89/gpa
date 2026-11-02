using System;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 캐릭터 생성 화면: 얼굴, 이름, 스탯 분배.
    public class CreateScreen
    {
        public static readonly string[] StatNames = { "외공", "내공", "경공", "근골", "오성", "인덕", "기연" };
        static readonly string[] StatHelp =
        {
            "강(剛) 무공 위력", "모든 무공 위력", "속도, 쾌(快) 무공 위력, 회피", "체력·내력, 유(柔) 무공 위력, 피로",
            "숙련도, 경지 돌파", "대화, 호감도", "치명타, 기연",
        };

        public readonly VisualElement Root = Ui.Box("fill", "landscape");
        readonly Stats stats = CharacterCreation.NewStats();
        readonly TextField nameField = new() { maxLength = CharacterCreation.MaxNameLength };
        readonly VisualElement looks = Ui.Box("row"), statList = Ui.Box("col");
        readonly Label remaining = Ui.Text("", "heading");
        readonly Action<string, int, Stats> onStart;
        Button start;
        int look;

        public CreateScreen(Action<string, int, Stats> onStart, Action onLoad)
        {
            this.onStart = onStart;
            var panel = Ui.Box("create", "panel");
            panel.Add(Ui.Text("새 유랑객", "heading"));
            panel.Add(looks);

            var nameRow = Ui.Box("row");
            nameRow.Add(Ui.Text("이름", "stat-name"));
            nameField.style.width = 300;
            nameField.RegisterValueChangedCallback(_ => Refresh());
            nameRow.Add(nameField);
            panel.Add(nameRow);

            panel.Add(remaining);
            panel.Add(statList);

            var buttons = Ui.Box("row");
            start = Ui.Btn("강호로 나선다", () => onStart(nameField.value, look, stats), false, "grow");
            buttons.Add(start);
            buttons.Add(Ui.Btn("불러오기", onLoad));
            panel.Add(buttons);
            var titleCol = Ui.Box("title-col");
            titleCol.Add(Ui.Text("江\n湖\n流\n浪\n記", "title-hanja"));
            titleCol.Add(Ui.Box("seal"));
            Root.Add(titleCol);
            Root.Add(panel);
            Refresh();
        }

        void Refresh()
        {
            looks.Clear();
            for (int i = 0; i < CharacterCreation.AppearanceCount; i++)
            {
                int index = i;
                var box = Ui.Box("face-box", "look");
                box.EnableInClassList("look-selected", i == look);
                box.Add(Ui.Image(Ui.Portrait(Ui.HeroPortrait(i), CharacterCreation.Looks[i]), "face"));
                box.RegisterCallback<ClickEvent>(_ =>
                {
                    look = index;
                    Sound.Play("Move1");
                    Refresh();
                });
                looks.Add(box);
            }

            remaining.text = $"남은 포인트 {CharacterCreation.Remaining(stats)}";
            statList.Clear();
            foreach (var k in CharacterCreation.All)
            {
                var row = Ui.Box("stat-row");
                row.Add(Ui.Text(StatNames[(int)k], "stat-name"));
                row.Add(Ui.Btn("−", () => Change(k, -1), CharacterCreation.CanLower(stats, k), "btn-small"));
                row.Add(Ui.Text(stats.Ref(k).ToString(), "stat-value"));
                row.Add(Ui.Btn("+", () => Change(k, 1), CharacterCreation.CanRaise(stats, k), "btn-small"));
                row.Add(Ui.Text(StatHelp[(int)k], "small", "muted"));
                statList.Add(row);
            }
            start.SetEnabled(CharacterCreation.IsValid(nameField.value, look, stats));
        }

        void Change(StatKind k, int delta)
        {
            stats.Ref(k) += delta;
            Refresh();
        }
    }
}
