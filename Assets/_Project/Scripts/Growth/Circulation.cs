using System;
using System.Linq;
using UnityEngine;

namespace GPA.Growth
{
    public enum Effort { Steady, Earnest, Reckless }                // 안정, 정진, 무리
    public enum CirculationEnd { Ongoing, Complete, Overheated, Dispersed } // 진행 중, 완주, 주화입마, 산기

    // 일주천 한 판. 화면 없이 돌아가는 규칙이다. 숨(누르면 빨라지고 떼면 느려진다)으로 기운의 속도를 구간마다 알맞게 맞춘다.
    // 너무 빠르면 화기가, 너무 느리면 산기가 찬다. 막힌 혈도는 세게 밀어야 뚫린다. 수치는 임시값 (docs/05_능력치.md).
    public class Circulation
    {
        public class Node
        {
            public string Name;
            public float X, Y; // 좌선 그림 위 위치 (0~1)
            public int MinRealm;
        }

        // 소주천 경로: 단전에서 등(독맥)을 타고 올라 앞(임맥)으로 내려온다. 경지가 오르면 혈도가 늘어난다.
        static readonly Node[] Route =
        {
            new() { Name = "단전", X = .52f, Y = .64f }, new() { Name = "회음", X = .46f, Y = .76f },
            new() { Name = "장강", X = .36f, Y = .72f }, new() { Name = "명문", X = .34f, Y = .56f },
            new() { Name = "영대", X = .35f, Y = .42f, MinRealm = 2 }, new() { Name = "대추", X = .41f, Y = .28f },
            new() { Name = "옥침", X = .44f, Y = .16f, MinRealm = 1 }, new() { Name = "백회", X = .52f, Y = .06f },
            new() { Name = "인당", X = .62f, Y = .14f }, new() { Name = "천돌", X = .56f, Y = .27f, MinRealm = 1 },
            new() { Name = "단중", X = .62f, Y = .40f }, new() { Name = "중완", X = .63f, Y = .52f, MinRealm = 2 },
        };

        const float Accel = 0.9f, Drag = 0.75f;    // 숨 쉴 때 속도 변화 (초당)
        const float Cool = 0.1f;                   // 알맞은 속도일 때 화기·산기가 가라앉는 양 (초당)
        const float PushHeat = 0.1f;               // 막힌 혈도로 밀어붙이는 동안 쌓이는 화기 (초당)
        public const float Burst = 0.8f;           // 막힌 혈도를 뚫는 속도
        static readonly float[] Wants = { .3f, .45f, .6f, .72f };

        public readonly Node[] Nodes;
        public readonly float[] Want;     // 구간마다 알맞은 속도
        public readonly bool[] Blocked;   // 이 구간 끝의 혈도가 막혔다
        public readonly float Width;      // 알맞은 속도의 허용 폭 (±)
        public readonly Effort Effort;
        readonly float flow, heatRate, scatterRate, tremor;
        float time, inBand;

        public float Speed { get; private set; } = 0.4f;
        public float Travel { get; private set; }   // 지나온 구간 수. 소수점이 구간 안의 위치
        public float Heat { get; private set; }     // 화기 0~1. 차면 주화입마
        public float Scatter { get; private set; }  // 산기 0~1. 차면 기운이 흩어진다
        public CirculationEnd Result { get; private set; }

        public Circulation(Effort effort, int realm, int wis, int con, bool innerInjured, float fatigueRatio, System.Random rng)
        {
            Effort = effort;
            Nodes = Route.Where(n => n.MinRealm <= realm).ToArray();
            int blocks = (int)effort + (innerInjured ? 1 : 0);
            Width = effort switch { Effort.Steady => .14f, Effort.Earnest => .1f, _ => .09f } + wis * .003f;
            heatRate = effort switch { Effort.Steady => .35f, Effort.Earnest => .5f, _ => .55f } / (1 + con / 20f);
            scatterRate = effort switch { Effort.Steady => .25f, Effort.Earnest => .35f, _ => .45f };
            flow = .7f * (1 + realm * .1f);
            tremor = fatigueRatio * .8f; // 지치면 숨이 흔들린다

            Want = new float[Nodes.Length];
            Blocked = new bool[Nodes.Length];
            int last = -1;
            for (int i = 0; i < Want.Length; i++)
            {
                int pick = i == 0 ? 1 : rng.Next(Wants.Length);
                if (pick == last) pick = (pick + 1) % Wants.Length;
                Want[i] = Wants[last = pick];
            }
            // 첫 두 구간과 마지막 구간은 막지 않는다. 숨을 고르고 끝을 맺는 자리다.
            for (int n = 0; n < blocks; n++)
            {
                int at = rng.Next(2, Want.Length - 1);
                Blocked[at] = true;
                Want[at] = Burst + .08f;
            }
        }

        // 지금 캐릭터 상태로 한 판을 만든다.
        public static Circulation For(Character h, Effort effort, System.Random rng)
        {
            var s = h.GearedStats;
            return new Circulation(effort, (int)h.Realm, s.Wis, s.Con, h.InnerInjured, h.Fatigue / (float)h.MaxFatigue, rng);
        }

        public int Segment => Math.Min((int)Travel, Want.Length - 1);
        public float WantNow => Want[Segment];
        public float Quality => time > 0 ? inBand / time : 0; // 알맞은 속도로 흐른 시간의 비율

        // 완주한 판의 보상 배율. 자동 수련이 1배다.
        public float Reward =>
            Result != CirculationEnd.Complete ? 0 : Effort switch { Effort.Steady => 1f, Effort.Earnest => 1.4f, _ => 2f } * (.7f + .7f * Quality);

        public void Tick(float dt, bool breathing)
        {
            if (Result != CirculationEnd.Ongoing) return;
            time += dt;
            Speed = Mathf.Clamp01(Speed + ((breathing ? Accel : -Drag) + tremor * Mathf.Sin(time * 7)) * dt);

            int seg = Segment;
            float want = Want[seg];
            if (Speed > want + Width) Heat += heatRate * dt;
            else if (Speed < want - Width) Scatter += scatterRate * dt;
            else
            {
                inBand += dt;
                Heat = Mathf.Max(0, Heat - Cool * dt);
                Scatter = Mathf.Max(0, Scatter - Cool * dt);
            }

            if (Blocked[seg]) Heat += PushHeat * dt;
            Travel += Speed * flow * dt;
            if (Blocked[seg] && Travel >= seg + 1)
            {
                if (Speed >= Burst) Blocked[seg] = false;
                else Travel = seg + .999f; // 뚫릴 때까지 혈도 앞에 멈춰 있다
            }

            if (Heat >= 1) Result = CirculationEnd.Overheated;
            else if (Scatter >= 1) Result = CirculationEnd.Dispersed;
            else if (Travel >= Want.Length) Result = CirculationEnd.Complete;
        }
    }
}
