using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 초식 연마 화면. 내려오는 먹점을 판정선에서 A S W D (또는 방향키, 칸 클릭)로 친다.
    public class FormScreen
    {
        const float HitLine = 590, PixelsPerSecond = 380, Slash = 0.18f, Flash = 0.1f;
        static readonly string[] Keys = { "← A", "↓ S", "↑ W", "→ D" };
        static readonly int[] LaneX = { 0, 1, 2, 3 };

        public readonly VisualElement Root = Ui.Box("fill", "dark-bg");
        readonly Character hero;
        readonly MartialArt art;
        readonly Action<string> onClose;
        readonly Sprite[] slash = Ui.Strip("FX/SpriteSheetSlash01", 5);
        internal FormPractice Sim;

        VisualElement figure, fx, scoreFill;
        readonly VisualElement[] lanes = new VisualElement[FormPractice.Lanes];
        readonly float[] laneFlash = new float[FormPractice.Lanes];
        readonly List<(FormPractice.Note note, VisualElement dot)> dots = new();
        Label form, judge;
        float slashTime;
        int misses;
        bool finished;

        public FormScreen(Character hero, MartialArt art, Action<string> onClose)
        {
            this.hero = hero;
            this.art = art;
            this.onClose = onClose;
            Root.focusable = true;
            ShowIntro();
        }

        void ShowIntro()
        {
            Root.Clear();
            Sim = null;
            finished = false;
            int gate = hero.FormGate(art);
            var box = Ui.Box("result");
            var panel = Ui.Box("panel", "circ-choice");
            panel.Add(Ui.Text($"{art.displayName} {gate}성", "title", "center"));
            panel.Add(Ui.Text("초식을 끝까지 이어 쳐라. 틀린 자리는 언제나 같은 자리다. 외워서 넘어서라.", "wrap"));
            panel.Add(Ui.Btn("연마한다", Begin));
            panel.Add(Ui.Btn("그만둔다", () => onClose("")));
            box.Add(panel);
            Root.Add(box);
        }

        internal void Begin()
        {
            Sim = new FormPractice(art, hero.FormGate(art), hero.GearedStats.Wis, hero.Fatigue / (float)hero.MaxFatigue);
            misses = 0;
            dots.Clear();
            Root.Clear();
            Root.Focus(); // 눌렀던 버튼에 포커스가 남으면 키가 버튼을 건드린다

            var layout = Ui.Box("circ");
            var side = Ui.Box("form-side");
            side.Add(Ui.Text(art.displayName, "title", "center"));
            form = Ui.Text("", "form-name");
            side.Add(form);
            figure = Ui.Box("form-figure");
            figure.style.backgroundImage = new StyleBackground(Ui.Body(Ui.HeroPortrait(hero.Appearance)));
            fx = Ui.Box("form-fx");
            fx.pickingMode = PickingMode.Ignore;
            figure.Add(fx);
            side.Add(figure);
            var score = Ui.Bar("bar-hp");
            scoreFill = score.fill;
            var need = Ui.Box("form-need");
            need.style.left = Length.Percent(Sim.Need * 100);
            score.bar.Add(need);
            side.Add(Ui.Text($"숙달: 표시까지 채워라 ({Sim.Need:P0})", "small"));
            side.Add(score.bar);
            layout.Add(side);

            var board = Ui.Box("form-board", "panel");
            for (int i = 0; i < lanes.Length; i++)
            {
                int lane = i;
                lanes[i] = Ui.Box("form-lane");
                lanes[i].RegisterCallback<PointerDownEvent>(_ => Press(lane));
                lanes[i].Add(Ui.Text(Keys[i], "form-key"));
                board.Add(lanes[i]);
            }
            var line = Ui.Box("form-line");
            line.pickingMode = PickingMode.Ignore;
            board.Add(line);
            judge = Ui.Text("", "form-judge");
            judge.pickingMode = PickingMode.Ignore;
            board.Add(judge);
            foreach (var n in Sim.Notes)
            {
                var dot = Ui.Box("form-note");
                dot.pickingMode = PickingMode.Ignore;
                lanes[n.Lane].Add(dot);
                dots.Add((n, dot));
            }
            layout.Add(board);
            Root.Add(layout);
            Draw();
        }

        public void Press(int lane)
        {
            if (Sim == null || finished) return;
            laneFlash[lane] = Flash;
            var hit = Sim.Press(lane);
            if (hit == Hit.None) return;
            judge.text = hit == Hit.Perfect ? "정확" : "좋음";
            slashTime = Slash;
            Sound.Play(hit == Hit.Perfect ? "Slash" : "Hit1");
        }

        public void Tick(float dt)
        {
            if (Sim == null || finished) return;
            Sim.Tick(dt);
            if (Sim.Misses > misses)
            {
                misses = Sim.Misses;
                judge.text = "놓침";
                Sound.Play("Cancel");
            }
            slashTime = Mathf.Max(0, slashTime - dt);
            for (int i = 0; i < laneFlash.Length; i++) laneFlash[i] = Mathf.Max(0, laneFlash[i] - dt);
            Draw();
            if (Sim.Done) Finish();
        }

        void Draw()
        {
            foreach (var (note, dot) in dots)
            {
                float y = HitLine - (note.Time - Sim.Clock) * PixelsPerSecond;
                dot.style.display = note.Hit == Hit.None && y > -30 ? DisplayStyle.Flex : DisplayStyle.None;
                dot.style.top = y;
            }
            for (int i = 0; i < lanes.Length; i++) lanes[i].EnableInClassList("form-lane-on", laneFlash[i] > 0);
            form.text = Sim.Clock < FormPractice.LeadIn - 1 ? "호흡을 고른다…" : Sim.CurrentForm;
            Ui.SetBar(scoreFill, Sim.Score);
            figure.style.translate = new Translate(slashTime / Slash * 40, 0);
            fx.style.backgroundImage = slashTime > 0
                ? new StyleBackground(slash[Mathf.Clamp((int)((1 - slashTime / Slash) * slash.Length), 0, slash.Length - 1)])
                : new StyleBackground(StyleKeyword.None);
        }

        void Finish()
        {
            finished = true;
            bool passed = Sim.Passed;
            int gate = Sim.Gate;
            var after = passed && gate == 10 ? art.ascendsTo : null;
            hero.ApplyForm(Sim);
            Sound.Play(passed ? "Bonus" : "Cancel");
            var box = Ui.Box("result");
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text(after != null ? after.displayName : passed ? $"{art.displayName} {gate}성" : "초식이 흐트러졌다", "title", "center"));
            if (after != null) panel.Add(Ui.Text($"{art.displayName}의 끝에서 모든 갈래가 하나로 돌아왔다.", "center", "wrap"));
            panel.Add(Ui.Text($"숙달 {Sim.Score:P0}   정확 {Sim.Perfects}   좋음 {Sim.Goods}   놓침 {Sim.Misses}", "center"));
            if (!passed) panel.Add(Ui.Btn("다시 연마한다", ShowIntro, hero.CanTrain(Training.Sparring)));
            panel.Add(Ui.Btn("계속", () => onClose(after != null ? $"{art.displayName}이 {after.displayName}이 되었다!" : passed ? $"{art.displayName}이 {gate}성에 올랐다." : "연마에 실패했다.")));
            box.Add(panel);
            Root.Add(box);
        }
    }
}
