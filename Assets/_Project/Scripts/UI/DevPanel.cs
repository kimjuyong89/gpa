#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using GPA.Prototype;
using GPA.World;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 개발 패널 (F1). 기능을 바로 시험한다. 에디터와 개발 빌드에만 들어간다.
    public class DevPanel
    {
        public readonly VisualElement Root = Ui.Box("fill", "dim");
        readonly TownView town;
        readonly ScrollView list = new();
        readonly Label status = Ui.Text("", "small", "wrap");
        VisualElement row;

        Character Hero => town.state.Hero;

        public DevPanel(TownView town)
        {
            this.town = town;
            var panel = Ui.Box("menu", "panel");
            var top = Ui.Box("menu-tabs");
            top.Add(Ui.Text("개발 패널 (F1)", "heading"));
            top.Add(Ui.Box("grow"));
            top.Add(Ui.Btn("닫기", town.ToggleDev));
            panel.Add(top);
            panel.Add(status);
            list.AddToClassList("item-list");
            panel.Add(list);
            Root.Add(panel);
            Draw();
        }

        void Section(string title)
        {
            list.Add(Ui.Text(title, "heading"));
            row = Ui.Box("dev-row");
            list.Add(row);
        }

        // 패널을 띄운 채 값을 바꾼다.
        void Set(string label, Action act) => row.Add(Ui.Btn(label, () =>
        {
            act();
            Draw();
        }, true, "dev-btn"));

        // 패널을 닫고 화면을 연다.
        void Go(string label, Action act) => row.Add(Ui.Btn(label, () => town.Queue(() =>
        {
            town.ToggleDev();
            act();
        }), true, "dev-btn"));

        void Draw()
        {
            list.Clear();
            var h = Hero;
            status.text = $"{MenuScreen.RealmNames[(int)h.Realm]}   내공 {h.InnerPower}/{h.InnerPowerToBreak}   피로 {h.Fatigue}/{h.MaxFatigue}   돈 {h.Money}"
                + $"   동료 {town.state.CompanionCount}/{GameState.MaxCompanions}   내상 {(h.InnerInjured ? "있음" : "없음")}   깨달음 {(h.Enlightened ? "있음" : "없음")}   소속 {town.state.Sect ?? "없음"}";

            if (town.mode == TownView.Mode.Fight)
            {
                Section("지금 전투");
                Go("즉시 승리", () => town.EndFight(Outcome.Victory));
                Go("즉시 패배", () => town.EndFight(Outcome.Defeat));
                return;
            }

            DrawHero();
            DrawSect();
            DrawItems();

            Section("수련");
            Go("일주천: 안정", () => Circulate(Effort.Steady));
            Go("일주천: 정진", () => Circulate(Effort.Earnest));
            Go("일주천: 무리", () => Circulate(Effort.Reckless));
            if (h.Realm != Realm.Hwagyeong) Go("폐관수련: 심마", town.StartClosedDoor);

            Section("초식 연마");
            foreach (var art in town.AllArts.Where(a => a.forms.Length > 0))
                foreach (int gate in new[] { 6, 9, 10 })
                    Go($"{art.displayName} {gate}성", () =>
                    {
                        Hero.SetMasteryForTest(art, gate - 1);
                        town.OpenForm(art);
                        town.Form.Begin();
                    });

            Section("만류귀종");
            foreach (var art in town.AllArts.Where(a => a.ascendsTo != null))
                Set($"{art.ascendsTo.displayName} 바로 얻기", () =>
                {
                    Hero.SetMasteryForTest(art, 9);
                    Hero.PassForm(art);
                });

            Section("전투");
            foreach (var b in town.battles) Go(b.id, () => town.StartBattle(b.id, false));

            Section("대화·상점");
            foreach (var npc in town.world.nodes.Where(n => n.region != null).SelectMany(n => n.region.npcs))
                Go(npc.name, () => town.Talk(npc.knot));

            Section("이동");
            foreach (var node in town.world.nodes.Where(n => n.region != null)) Go(node.id, () => town.Goto(node.id));

            Section("산중 수련");
            Set("추락 조건 맞추기", () =>
            {
                Hero.Stats.Luck = Math.Max(Hero.Stats.Luck, 7);
                Hero.Stats.Light = Math.Max(Hero.Stats.Light, 10);
                town.state.Set("산중", 0);
                town.state.Set("첫전투", 0);
            });
            Go("추락 장면 보기", () => town.Talk("무명산_추락"));
            Go("무명산에 들어가기", () =>
            {
                town.state.EnterMountain();
                town.Goto(GameState.MountainNode);
            });
            if (town.state.InMountain)
            {
                Set("허기·체온 채우기", () =>
                {
                    town.state.Set("산중.허기", 100);
                    town.state.Set("산중.체온", 100);
                });
                Set("식량·장작·가죽 +10", () =>
                {
                    foreach (var key in new[] { "산중.식량", "산중.장작", "산중.가죽" }) town.state.Add(key, 10);
                });
                Set("하루 보내기", town.state.SleepInMountain);
                Set("허기 0 (강제 하산)", () => town.state.Set("산중.허기", 0));
            }

            Section("이야기");
            Set("장부를 손에 넣은 상태로", () =>
            {
                town.state.Set("장부", 1);
                town.state.SetQuest("대사형의 장부", 3, "개발 패널로 장부를 얻었다.");
            });
        }

        void DrawHero()
        {
            var h = Hero;
            Section("주인공");
            Set("돈 +1000", () => h.Money += 1000);
            Set("피로 0", () => h.Fatigue = 0);
            Set("피로 가득", () => h.Fatigue = h.MaxFatigue);
            Set("내공 가득", () => h.InnerPower = h.InnerPowerToBreak);
            Set("내상 전환", () => h.InnerInjured = !h.InnerInjured);
            Set("깨달음 전환", () => h.Enlightened = !h.Enlightened);
            Set("경지 올림", () => h.Realm = (Realm)Math.Min((int)h.Realm + 1, (int)Realm.Hwagyeong));
            Set("경지 내림", () => h.Realm = (Realm)Math.Max((int)h.Realm - 1, 0));
            Set("스탯 전부 +5", () =>
            {
                foreach (var k in CharacterCreation.All) h.Stats.Ref(k) += 5;
            });
            foreach (var c in town.companions)
                Set($"{c.name} 영입·방출", () =>
                {
                    if (town.state.Get("동료." + c.name) > 0) town.state.Dismiss(c.name);
                    else town.state.Recruit(c.name);
                });
        }

        void DrawSect()
        {
            var state = town.state;
            Section("문파·무공");
            foreach (var s in state.SectDb.Values)
            {
                Set($"{s.id} 입문", () => state.Join(s.id));
                Set($"{s.id} 직전제자", () =>
                {
                    if (state.Sect != s.id) state.Join(s.id);
                    state.Set("호감." + s.master, s.discipleAffinity);
                    state.BecomeDisciple();
                });
            }
            foreach (var art in town.AllArts.Where(Character.IsMental))
                Set($"운용: {art.displayName}", () =>
                {
                    if (!Hero.Arts.Contains(art)) Hero.Arts.Add(art);
                    Hero.ActiveMind = art;
                });
            Set("기예 전부 +20", () =>
            {
                foreach (SkillKind k in Enum.GetValues(typeof(SkillKind)))
                    if (k != SkillKind.None) Hero.SkillLevels[k] = Hero.Skill(k) + 20;
            });
            Set("무공 전부 배우기", () =>
            {
                foreach (var art in town.AllArts)
                    if (!Hero.Arts.Contains(art)) Hero.Arts.Add(art);
            });
        }

        void DrawItems()
        {
            var h = Hero;
            Section("아이템");
            Set("장비 전부", () =>
            {
                foreach (var i in ItemData.All.Where(i => i.IsGear)) h.Bag.Add(i);
            });
            Set("약·영약 전부 5개", () =>
            {
                foreach (var i in ItemData.All.Where(i => !i.IsGear)) h.Bag.Add(i, 5);
            });
            Set("소지품 비우기", () =>
            {
                for (int i = 0; i < h.Equipped.Length; i++) h.Unequip(i);
                h.Bag.counts.Clear();
            });
        }

        void Circulate(Effort effort)
        {
            town.OpenCirculation();
            town.Circulation.Begin(effort);
        }
    }
}
#endif
