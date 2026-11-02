using System;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 수련·정비 메뉴. 탭: 상태 / 소지품 / 진형 / 수련 / 일지 / 저장.
    public class MenuScreen
    {
        public static readonly string[] RealmNames = { "삼류", "이류", "일류", "절정", "초절정", "화경" };
        static readonly string[] Tabs = { "상태", "소지품", "진형", "수련", "일지", "저장" };
        static readonly string[] SlotNames = { "무기", "옷", "신발", "장갑", "장신구", "장신구" };

        public readonly VisualElement Root = Ui.Box("fill", "dim");
        readonly GameState state;
        string[] party;
        string picked; // 진형 탭에서 고른 사람
        readonly bool atTown;
        readonly Action<Training, MartialArt> onTrain;
        readonly Action<MartialArt> onForm;
        readonly Action onRest, onBreakthrough, onSave, onLoad, onClose;
        readonly VisualElement tabs = Ui.Box("menu-tabs"), body = Ui.Box("grow");
        readonly float breakthroughBonus;
        public string Message = "";
        int tab;

        public MenuScreen(GameState state, string[] party, bool atTown, float breakthroughBonus, Action<Training, MartialArt> onTrain, Action<MartialArt> onForm,
            Action onRest, Action onBreakthrough, Action onSave, Action onLoad, Action onClose)
        {
            this.state = state;
            this.party = party;
            this.atTown = atTown;
            this.breakthroughBonus = breakthroughBonus;
            this.onTrain = onTrain;
            this.onForm = onForm;
            this.onRest = onRest;
            this.onBreakthrough = onBreakthrough;
            this.onSave = onSave;
            this.onLoad = onLoad;
            this.onClose = onClose;

            var panel = Ui.Box("menu", "panel");
            panel.Add(tabs);
            panel.Add(body);
            Root.Add(panel);
            Refresh();
        }

        public void Refresh()
        {
            tabs.Clear();
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                tabs.Add(Ui.Tab(Tabs[i], i == tab, () =>
                {
                    tab = index;
                    Message = "";
                    Refresh();
                }));
            }
            tabs.Add(Ui.Box("grow"));
            tabs.Add(Ui.Btn("닫기", onClose, true, "btn-small"));

            body.Clear();
            if (Message != "") body.Add(Ui.Text(Message, "good"));
            switch (tab)
            {
                case 0: DrawStatus(); break;
                case 1: DrawBag(); break;
                case 2: DrawFormation(); break;
                case 3: DrawTraining(); break;
                case 4: DrawJournal(); break;
                default: DrawSystem(); break;
            }
        }

        void DrawStatus()
        {
            var h = state.Hero;
            var top = Ui.Box("row");
            var who = Ui.Box("face-box");
            who.Add(Ui.Image(Ui.Portrait(Ui.HeroPortrait(h.Appearance), CharacterCreation.Looks[h.Appearance]), "face"));
            top.Add(who);
            var info = Ui.Box("col");
            info.style.marginLeft = 16;
            info.Add(Ui.Text($"{h.Name}  ({RealmNames[(int)h.Realm]})", "heading"));
            string sect = state.Sect == null ? "무소속" : state.Disciple ? $"{state.Sect} 직전제자" : $"{state.Sect} 제자";
            info.Add(Ui.Text($"소속: {sect}"));
            if (h.ActiveMind != null) info.Add(Ui.Text($"내공: {h.ActiveMind.displayName} ({Combatant.NatureLabel(h.ActiveMind.nature)})"));
            info.Add(Ui.Text($"돈 {h.Money}냥   피로 {h.Fatigue}/{h.MaxFatigue}   내공 {h.InnerPower}/{h.InnerPowerToBreak}"));
            if (h.InnerInjured) info.Add(Ui.Text("내상을 입었다. 쉬어야 한다.", "warn"));
            if (party.Length > 0) info.Add(Ui.Text("동료: " + string.Join(", ", party)));
            top.Add(info);
            body.Add(top);

            var geared = h.GearedStats;
            body.Add(Ui.Text(string.Join("   ", CharacterCreation.All.Select(k =>
            {
                int gear = geared.Ref(k) - h.Stats.Ref(k);
                return $"{CreateScreen.StatNames[(int)k]} {h.Stats.Ref(k)}" + (gear > 0 ? $"+{gear}" : "");
            }))));
            body.Add(Ui.Text("무공", "heading"));
            body.Add(Ui.Text(string.Join("   ", h.Arts.Select(a => $"{a.displayName} {h.Mastery(a)}성")), "wrap"));

            var skills = ((SkillKind[])Enum.GetValues(typeof(SkillKind))).Where(k => k != SkillKind.None);
            body.Add(Ui.Text("기예", "heading"));
            body.Add(Ui.Text(string.Join("   ", skills.Select(k => $"{Combatant.SkillLabel(k)} {h.Skill(k)}")), "wrap"));

            var standing = state.Values.Where(kv => GameState.Alignments.Contains(kv.Key) || kv.Key.StartsWith("평판.")).OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key.Replace("평판.", "")} {kv.Value:+0;-0;0}");
            body.Add(Ui.Text("강호의 평판", "heading"));
            body.Add(Ui.Text(string.Join("   ", standing), "wrap"));
        }

        void DrawBag()
        {
            var h = state.Hero;
            var split = Ui.Box("bag");
            var worn = Ui.Box("bag-worn");
            worn.Add(Ui.Text("장비", "heading"));
            for (int i = 0; i < h.Equipped.Length; i++)
            {
                int slot = i;
                var item = h.Equipped[i];
                worn.Add(Ui.Btn($"{SlotNames[i]}: {(item != null ? item.name : "없음")}", () =>
                {
                    h.Unequip(slot);
                    Refresh();
                }, item != null));
            }
            worn.Add(Ui.Text($"공격 +{h.Worn.Sum(i => i.attack)}%   방어 +{h.Worn.Sum(i => i.defense)}%", "small"));
            worn.Add(Ui.Text($"맞는 무기를 들면 그 무공의 위력이 {(Combatant.WeaponMatch - 1) * 100:0}% 더 오른다.", "small", "muted", "wrap"));
            split.Add(worn);

            var list = new ScrollView();
            list.AddToClassList("item-list");
            if (!h.Bag.Items.Any()) list.Add(Ui.Text("가방이 비어 있다.", "muted"));
            foreach (var item in h.Bag.Items.ToList())
            {
                var row = ShopScreen.Row(item, h.Bag.Count(item));
                if (item.IsGear)
                    row.Add(Ui.Btn("장착", () =>
                    {
                        h.Equip(item);
                        Refresh();
                    }, true, "item-btn"));
                else
                    row.Add(Ui.Btn(h.CanUse(item) ? "사용" : item.kind == ItemKind.Consumable && item.fatigue == 0 ? "전투용" : "못 쓴다", () =>
                    {
                        var realm = h.Realm;
                        h.Use(item);
                        Sound.Play("Bonus");
                        Message = h.Realm != realm ? $"경지를 돌파했다! {RealmNames[(int)h.Realm]}에 올랐다." : $"{item.name}의 기운이 온몸에 퍼진다.";
                        Refresh();
                    }, h.CanUse(item), "item-btn"));
                list.Add(row);
            }
            split.Add(list);
            body.Add(split);
        }

        void DrawFormation()
        {
            var cells = state.Formation(party);
            string Label(string key) => key == GameState.HeroKey ? state.Hero.Name : key;
            body.Add(Ui.Text(picked == null ? "옮길 사람을 고르세요." : $"{Label(picked)}: 옮길 칸을 고르세요.", "heading"));
            body.Add(Ui.Text("오른쪽이 적이 있는 쪽이다. 같은 가로줄의 전열이 살아 있으면 후열은 근접 공격을 받지 않는다.", "small", "muted", "wrap"));
            var head = Ui.Box("grid-head");
            head.Add(Ui.Text("후열", "small"));
            head.Add(Ui.Text("전열", "small"));
            body.Add(head);
            for (int lane = 0; lane < Battle.LaneCount; lane++)
            {
                var row = Ui.Box("grid-row");
                foreach (int cell in new[] { lane + Battle.LaneCount, lane })
                {
                    var who = cells.FirstOrDefault(kv => kv.Value == cell).Key;
                    var button = Ui.Btn(who != null ? Label(who) : "빈 칸", () =>
                    {
                        if (picked != null)
                        {
                            state.MoveInFormation(picked, cell, party);
                            picked = null;
                        }
                        else picked = who;
                        Refresh();
                    }, true, "grid-cell", who == picked && who != null ? "btn-selected" : "btn", who == null ? "grid-empty" : "btn");
                    row.Add(button);
                }
                body.Add(row);
            }

            body.Add(Ui.Text($"동료 {party.Length}/{GameState.MaxCompanions}", "heading"));
            var leave = Ui.Box("row");
            foreach (var name in party)
                leave.Add(Ui.Btn($"{name} 떠나보내기", () =>
                {
                    state.Dismiss(name);
                    party = party.Where(p => p != name).ToArray();
                    picked = null;
                    Message = $"{name}이(가) 일행을 떠났다. 찾아가면 다시 부를 수 있다.";
                    Refresh();
                }));
            body.Add(leave);
        }

        void DrawTraining()
        {
            var h = state.Hero;
            body.Add(Ui.Text($"피로 {h.Fatigue}/{h.MaxFatigue}   내공 {h.InnerPower}/{h.InnerPowerToBreak}   돈 {h.Money}냥"));
            var (bar, fill) = Ui.Bar("bar-fatigue");
            Ui.SetBar(fill, h.Fatigue / (float)h.MaxFatigue);
            body.Add(bar);

            body.Add(Ui.Text("수련", "heading"));
            var row = Ui.Box("row");
            row.Add(Ui.Btn("일주천\n내공", () => onTrain(Training.Tonap, null), h.CanTrain(Training.Tonap)));
            row.Add(Ui.Btn("독서\n오성", () => onTrain(Training.Reading, null), h.CanTrain(Training.Reading)));
            row.Add(Ui.Btn("단련·노동\n근골·돈", () => onTrain(Training.Labor, null), h.CanTrain(Training.Labor)));
            body.Add(row);

            body.Add(Ui.Text("대련", "heading"));
            var spar = Ui.Box("row");
            foreach (var art in h.Arts.Where(a => a.kind == ArtKind.Outer))
                spar.Add(Ui.Btn($"{art.displayName}\n{h.Mastery(art)}성", () => onTrain(Training.Sparring, art), h.CanTrain(Training.Sparring)));
            body.Add(spar);

            var minds = h.Arts.Where(Character.IsMental).ToList();
            if (minds.Count > 1)
            {
                body.Add(Ui.Text($"운용 심법: {h.ActiveMind.displayName}", "heading"));
                var mindRow = Ui.Box("row");
                foreach (var art in minds)
                    mindRow.Add(Ui.Btn($"{art.displayName}\n{LineageName(art.lineage)}·{Combatant.NatureLabel(art.nature)} {h.Mastery(art)}성", () =>
                    {
                        h.ActiveMind = art;
                        Message = $"이제 {art.displayName}을 운용한다.";
                        Refresh();
                    }, true, art == h.ActiveMind ? "btn-selected" : "btn"));
                body.Add(mindRow);
            }

            var gated = h.Arts.Where(a => h.FormGate(a) > 0).ToList(); // 경공·심법도 관문이 있다
            if (gated.Count > 0)
            {
                body.Add(Ui.Text("초식 연마", "heading"));
                var forms = Ui.Box("row");
                foreach (var art in gated)
                    forms.Add(Ui.Btn($"{art.displayName}\n{h.FormGate(art)}성 연마", () => onForm(art), h.CanTrain(Training.Sparring)));
                body.Add(forms);
            }

            body.Add(Ui.Text("정비", "heading"));
            var rest = Ui.Box("row");
            if (state.Sect != null && atTown) rest.Add(Ui.Btn("문파 거처에서 쉰다", onRest));
            if (h.CanAttemptBreakthrough) rest.Add(Ui.Btn($"폐관수련: 심마와 싸운다 (세기 {h.DemonStrength(breakthroughBonus):P0})", onBreakthrough));
            body.Add(rest);
        }

        static string LineageName(Lineage line) => line switch
        {
            Lineage.Orthodox => "정종",
            Lineage.Deviant => "사공",
            Lineage.Universal => "만류귀종",
            _ => "공용",
        };

        void DrawJournal()
        {
            if (state.Quests.Count == 0) body.Add(Ui.Text("아직 기록할 일이 없다.", "muted"));
            foreach (var q in state.Quests)
            {
                body.Add(Ui.Text(q.Done ? $"{q.Id} (완료)" : q.Id, "heading"));
                foreach (var note in q.Notes) body.Add(Ui.Text("· " + note, "wrap", "small"));
            }
        }

        void DrawSystem()
        {
            var row = Ui.Box("row");
            row.Add(Ui.Btn("저장", onSave));
            row.Add(Ui.Btn("불러오기", onLoad));
            body.Add(row);
        }
    }
}
