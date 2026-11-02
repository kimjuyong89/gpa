using System;
using System.Collections.Generic;
using GPA.Narrative;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 화면 아래 대화 상자. 한 줄씩 타자기처럼 찍고, Advance()로 넘긴다.
    // "# battle: <id>"나 "# shop: <id>" 태그가 달린 줄을 넘기면 그 화면을 열고, 끝나면 Resume()으로 이어 간다.
    public class DialogueBox
    {
        const float CharsPerSecond = 45;

        // 화자별 얼굴 그림. ponytail: 인물이 늘면 인물 데이터로 옮긴다
        static readonly Dictionary<string, string> Faces = new()
        {
            ["점소이"] = "Villager", ["흑사방 졸개"] = "NinjaDark", ["흑사방 연락책"] = "NinjaGray", ["소육"] = "Child",
            ["윤백산"] = "Master", ["탁웅"] = "Samurai", ["창고지기"] = "NinjaRed", ["고진악"] = "NinjaBlue", ["은둔 노인"] = "OldMan",
            ["장터 상인"] = "OldMan", ["석두"] = "Monk", ["운학"] = "FighterWhite", ["연아"] = "Woman",
        };

        public readonly VisualElement Root = Ui.Box("fill");
        readonly Dialogue dialogue;
        readonly string heroName, heroFace, heroPortrait;
        readonly Action<string> onBattle, onShop, onCirculate;
        readonly Action onEnd;

        readonly VisualElement faceBox = Ui.Box("face-box"), face = Ui.Box("face"), choices = Ui.Box("choices");
        readonly Label speaker = Ui.Text("", "speaker"), line = Ui.Text("", "line"), more = Ui.Text("▼", "more");
        string fullText = "";
        Action pending; // 이 줄을 넘기면 열 화면
        float shown;

        public DialogueBox(Dialogue dialogue, string knot, string heroName, string heroFace, string heroPortrait, Action<string> onBattle, Action<string> onShop, Action<string> onCirculate, Action onEnd)
        {
            this.dialogue = dialogue;
            this.heroName = heroName;
            this.heroFace = heroFace;
            this.heroPortrait = heroPortrait;
            this.onBattle = onBattle;
            this.onShop = onShop;
            this.onCirculate = onCirculate;
            this.onEnd = onEnd;

            var box = Ui.Box("dialogue", "panel");
            faceBox.Add(face);
            box.Add(faceBox);
            var text = Ui.Box("dialogue-text");
            text.Add(speaker);
            text.Add(line);
            box.Add(text);
            box.Add(more);
            Root.Add(box);
            Root.Add(choices);
            Root.RegisterCallback<ClickEvent>(_ => Advance());
            choices.RegisterCallback<ClickEvent>(e => e.StopPropagation());

            dialogue.Start(knot);
            Next();
        }

        bool Done => shown >= fullText.Length;

        // 다음 줄을 보여 준다. 빈 줄은 건너뛰고, 줄이 없으면 선택지를 띄우거나 끝낸다.
        void Next()
        {
            while (dialogue.CanContinue)
            {
                var (who, text) = Dialogue.Split(dialogue.Continue());
                if (text.Length == 0) continue;
                foreach (var tag in dialogue.Tags)
                {
                    string id = tag.Substring(tag.IndexOf(':') + 1).Trim();
                    if (tag.StartsWith("battle:")) pending = () => onBattle(id);
                    else if (tag.StartsWith("shop:")) pending = () => onShop(id);
                    else if (tag.StartsWith("circulate:")) pending = () => onCirculate(id);
                }
                Show(who, text);
                return;
            }
            if (dialogue.Choices.Count > 0) ShowChoices();
            else onEnd();
        }

        void Show(string who, string text)
        {
            bool hero = who == Dialogue.HeroSpeaker;
            speaker.text = hero ? heroName : who;
            line.style.unityFontStyleAndWeight = who.Length == 0 ? FontStyle.Italic : FontStyle.Normal;
            string look = hero ? heroFace : Faces.TryGetValue(who, out var f) ? f : null;
            faceBox.style.display = look != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (look != null) face.style.backgroundImage = new StyleBackground(Ui.Portrait(hero ? heroPortrait : who, look));
            fullText = text;
            shown = 0;
            line.text = "";
            more.visible = false;
            choices.Clear();
        }

        void ShowChoices()
        {
            more.visible = false;
            var list = dialogue.Choices;
            choices.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                choices.Add(Ui.Btn($"{i + 1}. {list[i]}", () => Choose(index)));
            }
        }

        // 선택지 번호 고르기 (클릭이나 숫자 키)
        public void Choose(int index)
        {
            if (index >= dialogue.Choices.Count || dialogue.CanContinue) return;
            choices.Clear();
            dialogue.Choose(index);
            Next();
        }

        // 화면 클릭이나 Space. 글자가 찍히는 중이면 끝까지 보여 주고, 아니면 다음으로.
        public void Advance()
        {
            if (!Done)
            {
                shown = fullText.Length;
                line.text = fullText;
                return;
            }
            if (choices.childCount > 0) return;
            Sound.Play("Move1");
            if (pending != null)
            {
                var open = pending;
                pending = null;
                open();
                return;
            }
            Next();
        }

        // 전투나 상점이 끝난 뒤 이어 간다.
        public void Resume() => Next();

        public void Tick(float dt)
        {
            if (Done)
            {
                more.visible = choices.childCount == 0;
                return;
            }
            shown = Mathf.Min(fullText.Length, shown + dt * CharsPerSecond);
            line.text = fullText.Substring(0, (int)shown);
        }
    }
}
