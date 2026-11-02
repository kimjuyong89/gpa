using System.Collections.Generic;
using System.Linq;

namespace GPA.Narrative
{
    // 일행과 진형. 관련 값 키: "동료.<이름>" "진형.<이름>"(칸 번호 + 1).
    // 칸 번호: 0~2 전열, 3~5 후열. 같은 가로줄은 3을 더한 칸이다.
    public partial class GameState
    {
        public const int MaxCompanions = 3;
        public const string HeroKey = "주인공";
        static readonly int[] FillOrder = { 1, 4, 0, 2, 3, 5 }; // 자리를 정하지 않은 사람이 설 칸

        public int CompanionCount => Values.Count(kv => kv.Key.StartsWith("동료.") && kv.Value > 0);
        public bool PartyFull => CompanionCount >= MaxCompanions;

        public bool Recruit(string name)
        {
            if (Get("동료." + name) > 0) return true;
            if (PartyFull) return false;
            Set("동료." + name, 1);
            return true;
        }

        public void Dismiss(string name) => Set("동료." + name, 0);

        // 주인공과 동료의 칸. 정해 둔 칸이 없거나 겹치면 빈 칸을 준다.
        public Dictionary<string, int> Formation(IEnumerable<string> companions)
        {
            var members = new[] { HeroKey }.Concat(companions).ToList();
            var cells = new Dictionary<string, int>();
            var taken = new HashSet<int>();
            foreach (var m in members)
                if (Get("진형." + m) is > 0 and var v && taken.Add(v - 1)) cells[m] = v - 1;
            foreach (var m in members.Where(m => !cells.ContainsKey(m)))
                cells[m] = FillOrder.First(taken.Add);
            return cells;
        }

        // who를 cell로 옮긴다. 그 칸에 있던 사람은 who가 있던 칸으로 간다.
        public void MoveInFormation(string who, int cell, IEnumerable<string> companions)
        {
            var f = Formation(companions);
            var other = f.FirstOrDefault(kv => kv.Value == cell).Key;
            if (other != null) f[other] = f[who];
            f[who] = cell;
            foreach (var kv in f) Set("진형." + kv.Key, kv.Value + 1);
        }
    }
}
