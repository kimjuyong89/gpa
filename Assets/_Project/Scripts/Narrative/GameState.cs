using System.Collections.Generic;
using GPA.Growth;

namespace GPA.Narrative
{
    public class Quest
    {
        public string Id; // 일지에 보이는 이름이기도 하다
        public int Stage;
        public bool Done;
        public readonly List<string> Notes = new();
    }

    // 대화와 이벤트가 읽고 쓰는 게임 상태.
    // 수치는 문자열 키로 저장한다. 키 규칙(대본에서 쓰는 이름):
    //   정사 성향  "정파", "사파", "마교"
    //   평판      "평판.<문파>"   예: 평판.청운검문
    //   호감      "호감.<인물>"   예: 호감.윤백산
    //   그 외      아무 플래그     예: 장부_획득
    public partial class GameState
    {
        public Character Hero;
        public string Location; // 월드맵 위치 (WorldNode.id)
        public readonly Dictionary<string, int> Values = new();
        public readonly List<Quest> Quests = new();

        public GameState(Character hero, IEnumerable<SectData> sects = null)
        {
            Hero = hero;
            if (sects != null)
                foreach (var s in sects) SectDb[s.id] = s;
        }

        public int Get(string key) => Values.TryGetValue(key, out var v) ? v : 0;
        public void Set(string key, int value) => Values[key] = value;
        public void Add(string key, int delta) => Values[key] = Get(key) + delta;

        public Quest FindQuest(string id) => Quests.Find(q => q.Id == id);

        public void SetQuest(string id, int stage, string note)
        {
            var q = FindQuest(id);
            if (q == null) Quests.Add(q = new Quest { Id = id });
            q.Stage = stage;
            if (!string.IsNullOrEmpty(note)) q.Notes.Add(note);
        }

        public void CompleteQuest(string id, string note)
        {
            SetQuest(id, FindQuest(id)?.Stage ?? 0, note);
            FindQuest(id).Done = true;
        }
    }
}
