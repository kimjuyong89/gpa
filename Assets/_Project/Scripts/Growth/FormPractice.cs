using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;

namespace GPA.Growth
{
    public enum Hit { None, Perfect, Good, Miss } // 아직, 정확, 좋음, 놓침

    // 초식 연마 한 판: 6·9·10성 관문의 리듬게임. 화면 없이 돌아가는 규칙이다.
    // 박자는 무공의 강유쾌로, 음표 배치는 무공과 관문으로 정해져서 언제나 같다. 초식처럼 외워서 깬다. 수치는 임시값 (docs/05_능력치.md).
    public class FormPractice
    {
        public class Note
        {
            public float Time;
            public int Lane;
            public int Form; // 몇 번째 초식인가
            public Hit Hit;
        }

        public const int Lanes = 4;
        public const float LeadIn = 2.5f; // 첫 음표까지 숨 고를 시간
        const float EndHold = 1f;

        public readonly MartialArt Art;
        public readonly int Gate;
        public readonly float Need; // 통과에 필요한 점수
        public readonly string[] Forms;
        public readonly List<Note> Notes = new();
        readonly float perfect, good; // 판정 폭 (±초)

        public float Clock { get; private set; }

        public FormPractice(MartialArt art, int gate, int wis, float fatigueRatio)
        {
            Art = art;
            Gate = gate;
            bool final = gate == 10 && art.ascendsTo != null; // 만류귀종으로 가는 마지막 관문
            Need = final ? .9f : gate switch { 6 => .6f, 9 => .7f, _ => .8f };
            int total = final ? 48 : gate switch { 6 => 18, 9 => 26, _ => 34 };
            float tempo = final ? .6f : gate switch { 6 => 1f, 9 => .85f, _ => .72f };
            float steady = 1 - fatigueRatio * .3f; // 지치면 판정이 좁아진다
            perfect = (.07f + wis * .001f) * steady;
            good = (.15f + wis * .002f) * steady;

            Forms = art.forms.Length > 0 ? art.forms : new[] { "제1초", "제2초", "제3초" };
            int phrase = Math.Max(1, total / Forms.Length);
            var beat = Rhythm(art.affinity);
            var rng = new Random((art.displayName ?? "").Sum(c => c) * 31 + gate);
            float t = LeadIn;
            int last = -1;
            for (int i = 0; i < total; i++)
            {
                int lane = rng.Next(Lanes);
                if (lane == last) lane = (lane + 1) % Lanes;
                Notes.Add(new Note { Time = t, Lane = last = lane, Form = Math.Min(i / phrase, Forms.Length - 1) });
                t += beat[i % beat.Length] * tempo;
                if ((i + 1) % phrase == 0) t += .5f * tempo; // 초식 사이에 숨을 끊는다
            }
        }

        // 강은 묵직하게 끊고, 쾌는 몰아치고, 유는 끊기지 않고 흐른다.
        static float[] Rhythm(Affinity a) => a switch
        {
            Affinity.Gang => new[] { .6f, .6f, .3f, .3f, .6f, .6f },
            Affinity.Kwae => new[] { .22f, .22f, .22f, .44f },
            Affinity.Yu => new[] { .42f, .42f, .21f, .42f },
            _ => new[] { .4f, .3f, .3f },
        };

        public bool Done => Clock >= Notes[^1].Time + EndHold;
        public int Perfects => Notes.Count(n => n.Hit == Hit.Perfect);
        public int Goods => Notes.Count(n => n.Hit == Hit.Good);
        public int Misses => Notes.Count(n => n.Hit == Hit.Miss);
        public float Score => (Perfects + Goods * .6f) / Notes.Count;
        public bool Passed => Done && Score >= Need;

        // 지금 치고 있는 초식 이름.
        public string CurrentForm => Forms[(Notes.FirstOrDefault(n => n.Hit == Hit.None) ?? Notes[^1]).Form];

        public void Tick(float dt)
        {
            Clock += dt;
            foreach (var n in Notes)
                if (n.Hit == Hit.None && Clock > n.Time + good) n.Hit = Hit.Miss;
        }

        // 건반 하나를 누른다. 판정 안에 음표가 없으면 None.
        public Hit Press(int lane)
        {
            var n = Notes.FirstOrDefault(x => x.Hit == Hit.None && x.Lane == lane && Math.Abs(x.Time - Clock) <= good);
            if (n == null) return Hit.None;
            return n.Hit = Math.Abs(n.Time - Clock) <= perfect ? Hit.Perfect : Hit.Good;
        }
    }
}
