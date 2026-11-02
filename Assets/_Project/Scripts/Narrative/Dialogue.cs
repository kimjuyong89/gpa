using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;

namespace GPA.Narrative
{
    // Ink 대본 하나를 실행한다. 대본의 EXTERNAL 함수를 GameState에 연결한다.
    // 함수 목록은 Assets/_Project/Dialogue/_externals.ink 참고.
    public class Dialogue
    {
        public const string HeroSpeaker = "주인공"; // 대본의 화자 이름. 화면에는 플레이어가 정한 이름이 나온다

        readonly Story story;

        public Dialogue(string compiledJson, GameState state)
        {
            story = new Story(compiledJson);
            story.BindExternalFunction("get", (string key) => state.Get(key));
            story.BindExternalFunction("add", (string key, int delta) => state.Add(key, delta));
            story.BindExternalFunction("set", (string key, int value) => state.Set(key, value));
            story.BindExternalFunction("money", () => state.Hero.Money);
            story.BindExternalFunction("pay", (int amount) =>
            {
                if (state.Hero.Money < amount) return false;
                state.Hero.Money -= amount;
                return true;
            });
            story.BindExternalFunction("earn", (int amount) => { state.Hero.Money += amount; });
            story.BindExternalFunction("give", (string item, int amount) => state.Hero.Bag.Add(Combat.ItemData.Find(item), amount));
            story.BindExternalFunction("inner_power", (int amount) =>
            {
                state.Hero.InnerPower = System.Math.Min(state.Hero.InnerPowerToBreak, state.Hero.InnerPower + amount);
            });
            story.BindExternalFunction("rest_inn", () => state.Hero.Rest(atSect: false));
            story.BindExternalFunction("quest", (string id, int stage, string note) => state.SetQuest(id, stage, note));
            story.BindExternalFunction("quest_done", (string id, string note) => state.CompleteQuest(id, note));
            story.BindExternalFunction("quest_stage", (string id) => state.FindQuest(id)?.Stage ?? 0);
            story.BindExternalFunction("recruit", (string name) => state.Recruit(name));
            story.BindExternalFunction("party_full", () => state.PartyFull);
            story.BindExternalFunction("enter_mountain", () => state.EnterMountain());
            story.BindExternalFunction("spend", (int fatigue) => state.Spend(fatigue));
            story.BindExternalFunction("can_train", () => state.Hero.CanTrain(Growth.Training.Tonap));
            story.BindExternalFunction("eat", () => { state.Eat(); });
            story.BindExternalFunction("make_fire", () => { state.MakeFire(); });
            story.BindExternalFunction("sleep_mountain", () => state.SleepInMountain());
            story.BindExternalFunction("find_art", (string file) =>
            {
                var art = UnityEngine.Resources.Load<Combat.MartialArt>("Arts/" + file);
                if (!state.Hero.Arts.Contains(art)) state.Hero.Arts.Add(art);
            });
            story.BindExternalFunction("rank", () => (int)state.Rank);
            story.BindExternalFunction("rank_name", () => state.RankName);
            story.BindExternalFunction("ranking", () => state.Ranking);
            story.BindExternalFunction("promote_blocker", () => state.PromotionBlocker());
            story.BindExternalFunction("promote", () => state.Promote());
            story.BindExternalFunction("win_ranking", () => state.WinRanking());
            story.BindExternalFunction("sect", () => state.Sect ?? "");
            story.BindExternalFunction("join_blocker", (string id) => state.JoinBlocker(id));
            story.BindExternalFunction("join", (string id) => state.Join(id));
            story.BindExternalFunction("is_disciple", () => state.Disciple);
            story.BindExternalFunction("disciple", () => state.BecomeDisciple());
            story.BindExternalFunction("knows", (string art) => state.Knows(art));
            story.BindExternalFunction("can_learn", (string art) => state.CanLearn(art));
            story.BindExternalFunction("learn", (string art) => state.LearnSectArt(art));
            story.BindExternalFunction("secret_job", (int risk) => state.SecretJob(risk));
            story.BindExternalFunction("expel", () => state.Expel());
        }

        // 한 게임에 Dialogue 하나를 계속 쓴다. 그래야 한 번만 고를 수 있는 선택지(*)와 방문 기록이 이어진다.
        public void Start(string knot) => story.ChoosePathString(knot);
        public string SaveState() => story.state.ToJson();
        public void LoadState(string json) => story.state.LoadJson(json);
        public bool CanContinue => story.canContinue;
        public string Continue() => story.Continue().Trim();
        // 방금 Continue()한 줄의 태그. 예: "battle: 흑사방_졸개들"
        public List<string> Tags => story.currentTags;
        public List<string> Choices => story.currentChoices.Select(c => c.text).ToList();
        public void Choose(int index) => story.ChooseChoiceIndex(index);
        public bool Ended => !story.canContinue && story.currentChoices.Count == 0;

        // "이름: 대사" 형식이면 화자와 대사로 나눈다. 아니면 서술.
        public static (string speaker, string text) Split(string line)
        {
            int i = line.IndexOf(": ");
            return i > 0 ? (line[..i], line[(i + 2)..]) : ("", line);
        }
    }
}
