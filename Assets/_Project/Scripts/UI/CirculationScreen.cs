using System;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 일주천 미니게임. 난이도를 고르고, Space나 마우스를 누르고 떼서 기운의 속도를 맞춘다.
    public class CirculationScreen
    {
        const float ReadyTime = 1.2f;
        static readonly Color PathColor = new(0.12f, 0.1f, 0.09f, 0.35f), DoneColor = new(0.62f, 0.14f, 0.11f);

        public readonly VisualElement Root = Ui.Box("fill", "dark-bg");
        readonly GameState state;
        readonly System.Random rng;
        readonly Action<string> onClose;
        readonly StatKind? site; // 수련터에서 하면 그 스탯이 오른다
        internal Circulation Sim;
        internal bool PointerHeld;

        VisualElement figure, qi, band, marker, heatFill, scatterFill;
        VisualElement[] nodes;
        Label hint;
        float ready;
        int segment;

        public CirculationScreen(GameState state, System.Random rng, Action<string> onClose, StatKind? site = null)
        {
            this.site = site;
            this.state = state;
            this.rng = rng;
            this.onClose = onClose;
            Root.focusable = true;
            Root.RegisterCallback<PointerDownEvent>(_ => PointerHeld = true);
            Root.RegisterCallback<PointerUpEvent>(_ => PointerHeld = false);
            Root.RegisterCallback<PointerLeaveEvent>(_ => PointerHeld = false);
            ShowChoice();
        }

        string AutoKey => "일주천." + state.Hero.Realm;

        void ShowChoice()
        {
            var h = state.Hero;
            var box = Ui.Box("result");
            var panel = Ui.Box("panel", "circ-choice");
            panel.Add(Ui.Text("일주천", "title", "center"));
            panel.Add(Ui.Text("단전의 기운을 경맥을 따라 한 바퀴 돌린다. 어떤 마음가짐으로 임할까?", "wrap"));
            if (h.InnerInjured) panel.Add(Ui.Text("내상 때문에 막힌 혈도가 하나 더 생긴다.", "warn", "wrap"));
            if (h.Efficiency < 100) panel.Add(Ui.Text("지쳐서 숨이 흔들리고 효과가 반감된다.", "warn", "wrap"));
            panel.Add(Ui.Btn("안정: 넓은 호흡. 보상 보통", () => Begin(Effort.Steady)));
            panel.Add(Ui.Btn("정진: 막힌 혈도 1곳. 보상 많음", () => Begin(Effort.Earnest)));
            panel.Add(Ui.Btn("무리: 막힌 혈도 2곳, 좁은 호흡. 보상 큼. 주화입마 위험", () => Begin(Effort.Reckless)));
            var row = Ui.Box("row");
            if (state.Get(AutoKey) > 0 && site == null)
                row.Add(Ui.Btn($"자동 수련 (내공 +{h.TonapGain})", () =>
                {
                    h.Train(Training.Tonap);
                    onClose("일주천을 마쳤다.");
                }, h.CanTrain(Training.Tonap), "grow"));
            row.Add(Ui.Btn("그만둔다", () => onClose(""), true, "grow"));
            panel.Add(row);
            box.Add(panel);
            Root.Add(box);
        }

        internal void Begin(Effort effort)
        {
            Sim = Circulation.For(state.Hero, effort, rng);
            ready = ReadyTime;
            segment = 0;
            Root.Clear();
            Root.Focus(); // 눌렀던 버튼에 포커스가 남으면 Space가 버튼을 다시 누른다

            var layout = Ui.Box("circ");
            figure = Ui.Box("circ-figure");
            figure.pickingMode = PickingMode.Ignore;
            figure.generateVisualContent += DrawPath;
            nodes = new VisualElement[Sim.Nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = Sim.Nodes[i];
                nodes[i] = Ui.Box("circ-node");
                nodes[i].style.left = Length.Percent(n.X * 100);
                nodes[i].style.top = Length.Percent(n.Y * 100);
                nodes[i].Add(Ui.Text(n.Name, "circ-name"));
                figure.Add(nodes[i]);
            }
            qi = Ui.Box("circ-qi");
            figure.Add(qi);
            layout.Add(figure);

            var side = Ui.Box("circ-side", "panel");
            side.Add(Ui.Text("일주천", "title"));
            hint = Ui.Text("호흡을 가다듬는다…", "heading", "wrap");
            side.Add(hint);
            side.Add(Ui.Text("호흡 속도", "small"));
            var gauge = Ui.Box("circ-gauge");
            band = Ui.Box("circ-band");
            marker = Ui.Box("circ-marker");
            gauge.Add(band);
            gauge.Add(marker);
            side.Add(gauge);
            var heat = Ui.Bar("bar-heat");
            var scatter = Ui.Bar("bar-scatter");
            heatFill = heat.fill;
            scatterFill = scatter.fill;
            side.Add(Ui.Text("화기: 차면 주화입마", "small"));
            side.Add(heat.bar);
            side.Add(Ui.Text("산기: 차면 기운이 흩어진다", "small"));
            side.Add(scatter.bar);
            side.Add(Ui.Text("Space나 마우스를 누르면 빨라지고, 떼면 느려진다.\n막대를 붉은 칸 안에 두어라.", "small", "muted", "wrap"));
            layout.Add(side);
            Root.Add(layout);
            Draw();
        }

        public void Tick(float dt, bool key)
        {
            if (Sim == null || Sim.Result != CirculationEnd.Ongoing) return;
            if (ready > 0)
            {
                ready -= dt;
                return;
            }
            Sim.Tick(dt, key || PointerHeld);
            if (Sim.Segment != segment)
            {
                segment = Sim.Segment;
                Sound.Play("Move1");
            }
            Draw();
            if (Sim.Result != CirculationEnd.Ongoing) Finish();
        }

        void Draw()
        {
            float left = Mathf.Clamp01(Sim.WantNow - Sim.Width), right = Mathf.Clamp01(Sim.WantNow + Sim.Width);
            band.style.left = Length.Percent(left * 100);
            band.style.width = Length.Percent((right - left) * 100);
            marker.style.left = Length.Percent(Sim.Speed * 100);
            Ui.SetBar(heatFill, Sim.Heat);
            Ui.SetBar(scatterFill, Sim.Scatter);

            int seg = Sim.Segment;
            float t = Sim.Travel - seg;
            var from = Sim.Nodes[seg];
            var to = Sim.Nodes[(seg + 1) % Sim.Nodes.Length];
            qi.style.left = Length.Percent(Mathf.Lerp(from.X, to.X, t) * 100);
            qi.style.top = Length.Percent(Mathf.Lerp(from.Y, to.Y, t) * 100);
            qi.style.scale = new Scale(Vector2.one * (1 + Sim.Heat * .8f));
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].EnableInClassList("circ-blocked", Sim.Blocked[(i + nodes.Length - 1) % nodes.Length]);
                nodes[i].EnableInClassList("circ-passed", i <= seg);
            }
            if (ready <= 0)
                hint.text = Sim.Blocked[seg] ? $"{to.Name} 혈이 막혔다! 힘껏 밀어라!" : Sim.Heat > .7f ? "기혈이 끓어오른다!" : Sim.Scatter > .7f ? "기운이 흩어진다!" : $"→ {to.Name}";
            figure.MarkDirtyRepaint();
        }

        void DrawPath(MeshGenerationContext ctx)
        {
            if (Sim == null) return;
            var p = ctx.painter2D;
            var r = figure.contentRect;
            Vector2 At(int i)
            {
                var n = Sim.Nodes[i % Sim.Nodes.Length];
                return new Vector2(n.X * r.width, n.Y * r.height);
            }
            void Line(float upTo, Color color, float width)
            {
                p.strokeColor = color;
                p.lineWidth = width;
                p.BeginPath();
                p.MoveTo(At(0));
                int whole = (int)upTo;
                for (int i = 1; i <= whole; i++) p.LineTo(At(i));
                if (upTo > whole) p.LineTo(Vector2.Lerp(At(whole), At(whole + 1), upTo - whole));
                p.Stroke();
            }
            Line(Sim.Nodes.Length, PathColor, 3);
            if (Sim.Travel > 0) Line(Sim.Travel, DoneColor, 5);
        }

        void Finish()
        {
            var h = state.Hero;
            float quality = Sim.Quality;
            bool done = Sim.Result == CirculationEnd.Complete;
            int gain = h.ApplyCirculation(Sim, site);
            if (done) state.Set(AutoKey, 1);
            Sound.Play(done ? "Bonus" : "Impact");

            string title = Sim.Result switch
            {
                CirculationEnd.Complete => "일주천 완성",
                CirculationEnd.Overheated => "주화입마",
                _ => "기운이 흩어졌다",
            };
            string text = Sim.Result switch
            {
                CirculationEnd.Complete => $"내공 +{gain}   조식 {quality:P0}",
                CirculationEnd.Overheated => "기혈이 뒤틀려 내상을 입었다.",
                _ => "피로만 쌓였다.",
            };
            var box = Ui.Box("result");
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text(title, "title", "center"));
            panel.Add(Ui.Text(text, "center"));
            panel.Add(Ui.Btn("계속", () => onClose(done ? $"일주천을 마쳤다. 내공 +{gain}" : text)));
            box.Add(panel);
            Root.Add(box);
        }
    }
}
