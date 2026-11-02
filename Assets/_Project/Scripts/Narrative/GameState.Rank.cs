using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;

namespace GPA.Narrative
{
    public enum SectRank { Outer, Inner, Disciple, Elder } // 외문제자, 내문제자, 직전제자, 장로

    // 문파 안의 삶: 신분, 문규, 서열. 수치는 임시값 (docs/03_문파.md).
    // 관련 값 키: "신분.<문파>" "서열.<문파>"
    public partial class GameState
    {
        public const int InnerContribution = 30, ElderContribution = 300, RuleBreach = 15, MaxRanking = 9;
        static readonly string[] RankNames = { "외문제자", "내문제자", "직전제자", "장로" };

        public SectRank Rank => Sect == null ? SectRank.Outer : (SectRank)Math.Max(Get("신분." + Sect), Disciple ? (int)SectRank.Disciple : 0);
        public string RankName => RankNames[(int)Rank];
        public int Contribution => Sect == null ? 0 : Get("공헌." + Sect);
        public int Ranking => 10 - (Sect == null ? 0 : Get("서열." + Sect)); // 1위가 가장 높다
        public bool ElderDiscount => Rank == SectRank.Elder;

        // 다음 신분을 막는 이유. 올라갈 수 있으면 "", 장로면 "끝".
        public string PromotionBlocker()
        {
            var s = CurrentSect;
            if (s == null) return "없음";
            switch (Rank)
            {
                case SectRank.Outer:
                    return Contribution >= InnerContribution ? "" : $"공헌 {InnerContribution} 필요";
                case SectRank.Inner:
                    return Get("호감." + s.master) >= s.discipleAffinity ? "" : $"{s.master} 호감 {s.discipleAffinity} 필요";
                case SectRank.Disciple:
                    if (Hero.Realm < Realm.Ilryu) return "일류 경지 필요";
                    return Contribution >= ElderContribution ? "" : $"공헌 {ElderContribution} 필요";
                default:
                    return "끝";
            }
        }

        public bool Promote()
        {
            if (PromotionBlocker() != "") return false;
            if (Rank == SectRank.Inner) return BecomeDisciple();
            Set("신분." + Sect, (int)Rank + 1);
            return true;
        }

        // 쓰면 안 되는 계열의 무공을 전투에서 썼으면 발각이 쌓이고, 다 차면 들킨다. 들키면 참.
        public bool BreakRule(IEnumerable<MartialArt> used)
        {
            var s = CurrentSect;
            if (s == null || s.forbidden == Lineage.Common || !used.Any(a => a.lineage == s.forbidden)) return false;
            return SecretJob(RuleBreach);
        }

        // 사제 대련에서 이겼다. 서열이 오르고 공헌을 얻는다.
        public void WinRanking()
        {
            int reward = 5 + Get("서열." + Sect);
            Set("서열." + Sect, Math.Min(MaxRanking, Get("서열." + Sect) + 1));
            Add("공헌." + Sect, reward);
        }

        // 공헌으로 비고의 물건을 바꾼다. 장로는 30% 싸다.
        public int VaultPrice(ItemData item) => ElderDiscount ? item.contribution * 7 / 10 : item.contribution;

        public bool BuyFromVault(ItemData item)
        {
            if (Sect == null || item.shop != Sect || Contribution < VaultPrice(item)) return false;
            Add("공헌." + Sect, -VaultPrice(item));
            Hero.Bag.Add(item);
            return true;
        }
    }
}
