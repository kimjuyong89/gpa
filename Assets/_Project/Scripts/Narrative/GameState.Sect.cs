using System.Collections.Generic;
using System.Linq;
using GPA.Combat;

namespace GPA.Narrative
{
    // 문파 소속: 입문, 무공 전수, 직전제자, 이중 소속 발각, 파문.
    // 관련 값 키: "평판.<문파>" "공헌.<문파>" "발각.<문파>" "추격.<문파>" "배신.<문파>"
    public partial class GameState
    {
        public const int RejectGap = 3;           // 반대 성향이 문파 성향보다 이만큼 높으면 입문 거절
        public const int RejectReputation = -3;   // 평판이 이 이하면 입문 거절
        public const int NormalMasteryCap = 7;    // 일반 제자의 문파 무공 숙련도 상한
        public const int CaughtAt = 100;          // 발각 수치가 이만큼 쌓이면 들킨다
        public static readonly string[] Alignments = { "정파", "사파", "마교" };

        public readonly Dictionary<string, SectData> SectDb = new();
        public string Sect;   // 현재 소속 문파 id. 없으면 null
        public bool Disciple; // 직전제자 여부
        public readonly HashSet<string> Expelled = new();

        public SectData CurrentSect => Sect != null && SectDb.TryGetValue(Sect, out var s) ? s : null;

        // 입문을 막는 이유. 없으면 "". ("없음" "소속" "파문" "성향" "평판")
        public string JoinBlocker(string id)
        {
            if (!SectDb.TryGetValue(id, out var s)) return "없음";
            if (Sect == id) return "소속";
            if (Expelled.Contains(id)) return "파문";
            int own = Get(s.alignment);
            int other = Alignments.Where(a => a != s.alignment).Sum(Get);
            if (other - own >= RejectGap) return "성향";
            if (Get("평판." + id) <= RejectReputation) return "평판";
            return "";
        }

        public bool Join(string id)
        {
            if (JoinBlocker(id) != "") return false;
            if (Sect != null)
            {
                // 다른 문파로 공공연히 옮기면 원래 문파에는 배신이다.
                Add("평판." + Sect, -5);
                Set("배신." + Sect, 1);
            }
            var s = SectDb[id];
            Sect = id;
            Disciple = false;
            foreach (var sa in s.arts)
            {
                Hero.MasteryCaps[sa.art] = NormalMasteryCap;
                if (sa.contribution == 0) Learn(sa.art);
            }
            return true;
        }

        public bool Knows(string artName) => Hero.Arts.Any(a => a.displayName == artName);

        // 소속 문파의 무공을 공헌도를 내고 배운다.
        public bool LearnSectArt(string artName)
        {
            var sa = CurrentSect?.arts.FirstOrDefault(x => x.art.displayName == artName);
            if (sa == null || Get("공헌." + Sect) < sa.contribution) return false;
            Learn(sa.art);
            return true;
        }

        void Learn(MartialArt art)
        {
            if (!Hero.Arts.Contains(art)) Hero.Arts.Add(art);
        }

        // 사부의 호감이 충분하면 직전제자가 된다. 문파 무공 상한이 풀리고 비전 초식을 배운다.
        public bool BecomeDisciple()
        {
            var s = CurrentSect;
            if (s == null || Disciple || Get("호감." + s.master) < s.discipleAffinity) return false;
            Disciple = true;
            foreach (var sa in s.arts) Hero.MasteryCaps.Remove(sa.art);
            if (s.secretArt != null) Learn(s.secretArt);
            return true;
        }

        // 소속 문파 몰래 다른 문파의 일을 한다. 발각 수치가 쌓이고, 들키면 true.
        public bool SecretJob(int risk)
        {
            var s = CurrentSect;
            if (s == null) return false;
            Add("발각." + Sect, risk);
            if (Get("발각." + Sect) < CaughtAt) return false;
            Expel(s.onCaught == CaughtPenalty.Hunt);
            return true;
        }

        public void Expel(bool hunt = false)
        {
            if (Sect == null) return;
            if (hunt) Set("추격." + Sect, 1);
            Expelled.Add(Sect);
            Add("평판." + Sect, -5);
            Sect = null;
            Disciple = false;
        }
    }
}
