using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using GPA.UI;
using GPA.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace GPA.Prototype
{
    // M1 게임 흐름: 캐릭터 생성, 걸어 다니는 지도(마을·강호), 대화, 전투, 수련·정비 메뉴를 오간다.
    public class TownView : MonoBehaviour
    {
        public WorldMapData world;
        public TextAsset story;                 // Ink가 컴파일한 .json
        public SectData[] sects;
        public BattleData[] battles;
        public List<UnitSetup> companions = new(); // "동료.<이름>" 값이 1 이상이면 전투에 합류
        public MartialArt[] startingArts;

        internal enum Mode { Create, Region, Talk, Fight }

        internal GameState state;
        internal Mode mode;
        internal RegionView region; // 지금 노드의 지역맵. 없으면 null
        internal PanelSettings panelSettings;
        internal string saveFile = "save.json";
        internal VisualElement UiRoot => root;
        internal VisualElement DevRoot; // 개발 패널이 떠 있으면 그 화면
        internal DialogueBox TalkBox => talk;

        Dialogue dialogue;          // 게임 하나에 하나. 대본 진행 상태가 이어진다
        VisualElement root, screen, hud, toast, hudFatigue;
        Label hudLine;
        DialogueBox talk;
        MenuScreen menu;
        ShopScreen shop;
        internal CirculationScreen Circulation;
        internal FormScreen Form;
        Combatant heroUnit;
        Action<Outcome> endFight;   // 지금 전투를 끝내는 쪽. 조우·대화 전투와 심마가 다르다
        bool fallOnFlee;            // 이번 전투에서 달아나면 산으로 떨어진다
        string shownLocation;       // 지금 깔린 지도의 위치
        BattleData battleData;
        bool battleFromTalk;        // 대화 중 전투면 끝난 뒤 대화로 돌아간다
        readonly Queue<Action> queued = new();
        readonly System.Random rng = new();

        void Awake()
        {
            Sound.Init(gameObject);

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform);

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/GameTheme");
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;

            var go = new GameObject("UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            go.SetActive(true);
            root = doc.rootVisualElement;
            root.AddToClassList("game-root");

            ShowCreate();
        }

        void Show(VisualElement next)
        {
            screen?.RemoveFromHierarchy();
            screen = next;
            if (next != null) root.Insert(0, next);
        }

        void ShowCreate()
        {
            Sound.Music("Town");
            Show(new CreateScreen((name, look, stats) =>
                Queue(() =>
                {
                    NewGame(CharacterCreation.Create(name, look, stats, startingArts));
                    Notice("백로진에 도착했다.");
                }), () => Queue(Load)).Root);
        }

        internal IEnumerable<UnitSetup> Party => companions.Where(c => state.Get("동료." + c.name) > 0).Take(GameState.MaxCompanions);

        // 진형대로 세운다. 정해 둔 칸이 없으면 Formation이 빈 칸을 준다.
        void Deploy(List<Combatant> allies)
        {
            var cells = state.Formation(Party.Select(c => c.name));
            foreach (var a in allies)
            {
                int cell = cells[a == heroUnit ? GameState.HeroKey : a.Name];
                a.Row = (Row)(cell / Battle.LaneCount);
                a.Lane = cell % Battle.LaneCount;
            }
        }

        internal void NewGame(Character hero)
        {
            hero.Money = 50;
            hero.Bag.Add(ItemData.Find("금창약"), 2);
            state = new GameState(hero, sects) { Location = world.start };
            dialogue = new Dialogue(story.text, state);
            Arrive();
        }

        // ponytail: 슬롯 하나. 여러 슬롯·자동 저장은 타이틀 화면을 만들 때
        string SavePath => System.IO.Path.Combine(Application.persistentDataPath, saveFile);

        internal void Save()
        {
            if (state.Location == world.overworld) RememberSpot(region.Cell);
            System.IO.File.WriteAllText(SavePath, SaveGame.Write(state, dialogue.SaveState()));
            Notice("저장했다.");
        }

        internal void Load()
        {
            if (!System.IO.File.Exists(SavePath))
            {
                Notice("저장된 게임이 없다.");
                return;
            }
            string saved = System.IO.File.ReadAllText(SavePath);
            if (!SaveGame.IsCompatible(saved))
            {
                Notice("예전 버전의 저장 파일이라 불러올 수 없다.");
                return;
            }
            CloseMenu();
            var arts = AllArts.ToDictionary(a => a.name);
            state = new GameState(new Character("", new Stats()), sects);
            string storyState = SaveGame.Read(saved, state, n => arts.TryGetValue(n, out var a) ? a : null);
            dialogue = new Dialogue(story.text, state);
            dialogue.LoadState(storyState);
            if (world.Node(state.Location)?.region == null) state.Location = world.start;
            Arrive();
            Notice("불러왔다.");
        }

        internal IEnumerable<MartialArt> AllArts => startingArts
            .Concat(Resources.LoadAll<MartialArt>("Arts"))
            .Concat(sects.SelectMany(s => s.arts.Select(a => a.art).Append(s.secretArt)))
            .SelectMany(a => a == null ? new MartialArt[0] : new[] { a, a.ascendsTo })
            .Where(a => a != null)
            .GroupBy(a => a.name)
            .Select(g => g.First());

        void Update()
        {
            while (queued.Count > 0) queued.Dequeue()();

            var kb = Keyboard.current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (kb != null && kb.f1Key.wasPressedThisFrame) ToggleDev();
#endif
            if (mode == Mode.Talk && talk != null && shop == null && Circulation == null)
            {
                talk.Tick(Time.deltaTime);
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) talk.Advance();
                    for (int i = 0; i < 9; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) talk.Choose(i);
                }
            }
            Circulation?.Tick(Time.deltaTime, kb != null && kb.spaceKey.isPressed);
            if (Form != null && kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Form.Press(0);
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Form.Press(1);
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Form.Press(2);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Form.Press(3);
            }
            Form?.Tick(Time.deltaTime);
            if (Circulation == null && Form == null && DevRoot == null && kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            {
                if (menu != null) CloseMenu();
                else if (mode == Mode.Region) OpenMenu();
            }
            if (state != null && state.Collapsed && mode == Mode.Region && menu == null && Circulation == null) ForceDescent();
            if (region != null)
            {
                region.Paused = mode != Mode.Region || menu != null || Circulation != null || Form != null || DevRoot != null;
                region.Agile = state.Hero.MovementArt != null;
                region.Tired = state.Hero.Fatigue >= state.Hero.MaxFatigue;
                if (hudLine != null)
                {
                    hudLine.text = HudLine();
                    Ui.SetBar(hudFatigue, state.Hero.Fatigue / (float)state.Hero.MaxFatigue);
                    ApplyTheme();
                }
            }
        }

        internal void Queue(Action action) => queued.Enqueue(action);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 지도와 전투에서만 연다. 대화와 미니게임 도중에는 끼어들지 않는다.
        internal void ToggleDev()
        {
            if (DevRoot != null)
            {
                DevRoot.RemoveFromHierarchy();
                DevRoot = null;
                if (mode == Mode.Region) SetMode(Mode.Region); // HUD를 새로 고친다
                return;
            }
            if (state == null || mode is Mode.Create or Mode.Talk || menu != null || Circulation != null || Form != null) return;
            DevRoot = new DevPanel(this).Root;
            root.Add(DevRoot);
        }
#endif

        void Notice(string text)
        {
            toast?.RemoveFromHierarchy();
            var shown = toast = Ui.Box("toast");
            toast.pickingMode = PickingMode.Ignore;
            var label = Ui.Text(text, "toast-text");
            label.pickingMode = PickingMode.Ignore;
            toast.Add(label);
            root.Add(toast);
            toast.schedule.Execute(() => shown.RemoveFromHierarchy()).StartingIn(2500);
        }

        // 화면 전환. 지도는 걷는 중과 그 위의 대화에서 보이고, 전투 때만 숨긴다.
        void SetMode(Mode m)
        {
            mode = m;
            if (region != null) region.gameObject.SetActive(m is Mode.Region or Mode.Talk);
            hud?.RemoveFromHierarchy();
            hud = null;
            hudLine = null;
            if (m != Mode.Region) return;
            Show(null);
            Sound.Music(world.Node(state.Location).kind == NodeKind.Town ? "Town" : "World");
            ShowHud();
        }

        string HudLine()
        {
            var h = state.Hero;
            string tired = h.Fatigue >= h.MaxFatigue ? "  (지쳐서 느리다)" : "";
            if (state.InMountain) return $"{state.MountainDay}일째   허기 {state.Hunger}   체온 {state.Warmth}\n식량 {state.Food}   장작 {state.Firewood}   피로 {h.Fatigue}/{h.MaxFatigue}{tired}";
            return $"{h.Money}냥   피로 {h.Fatigue}/{h.MaxFatigue}{tired}";
        }

        // 프레임 색은 정사 성향을 따른다. 사파가 정파보다 높으면 청동, 아니면 옥.
        void ApplyTheme() => root.EnableInClassList("theme-bronze", state != null && state.Get("사파") > state.Get("정파"));

        void ShowHud()
        {
            var h = state.Hero;
            ApplyTheme();
            hud = Ui.Box("fill");
            hud.pickingMode = PickingMode.Ignore;
            var tone = Ui.Box("fill", "map-tone");
            tone.pickingMode = PickingMode.Ignore;
            hud.Add(tone);
            var box = Ui.Box("hud", "panel");
            var face = Ui.Box("face-box");
            face.Add(Ui.Image(Ui.Portrait(Ui.HeroPortrait(h.Appearance), CharacterCreation.Looks[h.Appearance]), "face"));
            box.Add(face);
            var info = Ui.Box("hud-info");
            var nameRow = Ui.Box("row");
            nameRow.Add(Ui.Text(h.Name, "hud-name"));
            nameRow.Add(Ui.Text(MenuScreen.RealmNames[(int)h.Realm] + (h.InnerInjured ? " · 내상" : ""), "hud-realm"));
            info.Add(nameRow);
            info.Add(Ui.Text(state.Location, "hud-line", "muted"));
            var money = Ui.Box("row");
            money.Add(Ui.Box("coin"));
            hudLine = Ui.Text(HudLine(), "hud-line");
            money.Add(hudLine);
            info.Add(money);
            var (bar, fill) = Ui.Bar("bar-fatigue");
            hudFatigue = fill;
            Ui.SetBar(fill, h.Fatigue / (float)h.MaxFatigue);
            info.Add(bar);
            box.Add(info);
            hud.Add(box);
            hud.Add(Ui.Btn("수련·정비 (Tab)", () => Queue(OpenMenu), true, "hud-menu"));
            string move = h.MovementArt != null ? "   Shift 달리기   F 도약" : "";
            string way = world.Node(state.Location).kind == NodeKind.Town ? "   왼쪽 길 끝으로 나가면 강호" : "";
            string dev = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            dev = "   F1 개발 패널";
#endif
            var hint = Ui.Text($"방향키/WASD 이동{move}   E/Space 대화{way}{dev}", "hint");
            hint.pickingMode = PickingMode.Ignore;
            hud.Add(hint);
            root.Add(hud);
        }

        // 지금 위치의 지도를 깐다. 큰 지도는 떠났던 자리에서 다시 시작한다.
        void Arrive()
        {
            shownLocation = state.Location;
            if (region != null) Destroy(region.gameObject);
            var data = world.Node(state.Location).region;
            bool onOverworld = state.Location == world.overworld && state.Values.ContainsKey(WorldX);
            Vector2Int? spot = onOverworld ? new Vector2Int(state.Get(WorldX), state.Get(WorldY)) : null;
            region = RegionView.Create(data, Ui.Sheet(CharacterCreation.Looks[state.Hero.Appearance]), spot);
            region.OnTalk += npc => Queue(() => Talk(npc.knot));
            region.OnLeap += () =>
            {
                Sound.Play("Jump");
                state.Hero.GainMastery(state.Hero.MovementArt, 5); // 뛸수록 경공이 는다
            };
            region.OnStep += Stepped;
            region.OnExit += () => Queue(() =>
            {
                if (state.InMountain)
                {
                    Descend();
                    return;
                }
                state.Location = world.overworld;
                Notice("강호로 나섰다.");
                Arrive();
            });
            SetMode(Mode.Region);
        }

        const string WorldX = "강호.x", WorldY = "강호.y"; // 큰 지도에서의 자리

        void RememberSpot(Vector2Int c)
        {
            state.Set(WorldX, c.x);
            state.Set(WorldY, c.y);
        }

        // 큰 지도에서 한 칸 걸었다: 입구면 들어가고, 아니면 피로와 조우를 처리한다.
        void Stepped(TileDef tile)
        {
            if (tile != null && tile.portal != "")
            {
                Queue(() => EnterPortal(tile.portal));
                return;
            }
            if (state.Location != world.overworld) return;
            string battle = world.Walk(state, tile, rng);
            if (battle == null) return;
            Queue(() =>
            {
                Notice("누군가 길을 막아선다!");
                StartBattle(battle, false);
            });
        }

        internal void EnterPortal(string nodeId)
        {
            var node = world.Node(nodeId);
            if (node.kind == NodeKind.Locked)
            {
                Notice($"{nodeId}: 아직 갈 수 없다.");
                region.StepBack();
                return;
            }
            RememberSpot(region.Previous);
            state.Location = nodeId;
            Notice($"{nodeId}에 도착했다.");
            Arrive();
        }

        internal void Talk(string knot)
        {
            SetMode(Mode.Talk);
            talk = new DialogueBox(dialogue, knot, state.Hero.Name, CharacterCreation.Looks[state.Hero.Appearance], Ui.HeroPortrait(state.Hero.Appearance),
                id => Queue(() => StartBattle(id, true)), id => Queue(() => OpenShop(id)),
                stat => Queue(() => OpenCirculation((StatKind)Enum.Parse(typeof(StatKind), stat))), () => Queue(() =>
                {
                    talk = null;
                    if (state.Location != shownLocation) Arrive(); // 대본이 자리를 옮겼다
                    else SetMode(Mode.Region);
                }));
            Show(talk.Root);
        }

        internal void OpenShop(string id)
        {
            shop = new ShopScreen(state.Hero, id, () => Queue(() =>
            {
                shop.Root.RemoveFromHierarchy();
                shop = null;
                talk.Resume();
            }));
            root.Add(shop.Root);
        }

        internal void StartBattle(string id, bool fromTalk)
        {
            var data = battles.FirstOrDefault(b => b.id == id);
            if (data == null)
            {
                Notice($"전투 데이터가 없다: {id}");
                if (fromTalk) Queue(() => talk.Resume());
                return;
            }
            battleData = data;
            battleFromTalk = fromTalk;
            fallOnFlee = state.MarkFirstBattle() && state.CanFallIntoMountain;
            heroUnit = state.Hero.ToCombatant(Row.Front);
            var allies = new List<Combatant> { heroUnit };
            if (!data.solo)
                allies.AddRange(Party.Select(c => c.Create((int)state.Hero.Realm)));
            Deploy(allies);
            endFight = EndBattle;
            SetMode(Mode.Fight);
            Sound.Music("Battle");
            var fight = new BattleScreen(new Battle(allies, data.enemies.Select(e => e.Create())) { Bag = state.Hero.Bag, AlwaysFlee = fallOnFlee }, !fromTalk || fallOnFlee, this, o => Queue(() => EndBattle(o)));
            Show(fight.Root);
        }

        // 결과는 "전투.<id>" 값(승리 1, 패배 -1)으로 남겨서 대본이 이어 받는다.
        internal void EndBattle(Outcome outcome)
        {
            StopAllCoroutines();
            var h = state.Hero;
            h.ApplyBattle(heroUnit);
            if (outcome == Outcome.Fled && fallOnFlee)
            {
                Talk("무명산_추락");
                return;
            }
            if (outcome == Outcome.Fled)
            {
                Notice("간신히 달아났다.");
                SetMode(Mode.Region);
                return;
            }
            bool won = outcome == Outcome.Victory;
            state.Set("전투." + battleData.id, won ? 1 : -1);
            if (won)
            {
                h.Money += battleData.reward;
                var gains = battleData.RollLoot(rng, h.GearedStats.Luck);
                foreach (var item in gains) h.Bag.Add(item);
                var names = gains.Select(i => i.name).ToList();
                if (battleData.reward > 0) names.Insert(0, $"{battleData.reward}냥");
                if (names.Count > 0)
                {
                    Sound.Play("Coin");
                    Notice(string.Join(", ", names) + " 획득");
                }
            }
            else
            {
                h.InnerInjured = true;
                string text = "정신을 잃었다가 겨우 깨어났다. (내상)";
                if (!battleFromTalk)
                {
                    h.Money /= 2; // 대화 밖 패배: 가진 돈 절반을 잃는다
                    text += " 전낭이 가벼워졌다.";
                }
                Notice(text);
            }
            if (battleFromTalk)
            {
                SetMode(Mode.Talk);
                Show(talk.Root);
                talk.Resume();
            }
            else SetMode(Mode.Region);
        }

        // 폐관수련: 혼자 들어가 심마와 싸운다. 약도 동료도 없고 달아날 수도 없다.
        internal void StartClosedDoor()
        {
            CloseMenu();
            var h = state.Hero;
            heroUnit = h.ToCombatant(Row.Front);
            var fight = new Battle(new[] { heroUnit }, new[] { h.InnerDemon(BreakthroughBonus) }) { Tempting = true };
            fight.Events.Add(new BattleEvent { Text = "폐관에 들어 눈을 감자, 내 얼굴을 한 그림자가 앞에 앉아 있다." });
            fight.Events.Add(new BattleEvent { Text = "심마: 그 힘으로 위에 오르겠다고?" });
            endFight = EndClosedDoor;
            SetMode(Mode.Fight);
            Sound.Music("Battle");
            Show(new BattleScreen(fight, false, this, o => Queue(() => EndClosedDoor(o))).Root);
        }

        internal void EndFight(Outcome outcome) => endFight(outcome);

        internal void EndClosedDoor(Outcome outcome)
        {
            StopAllCoroutines();
            var h = state.Hero;
            h.ApplyBattle(heroUnit);
            bool won = outcome == Outcome.Victory;
            h.FinishBreakthrough(won);
            string scar = h.InnerInjured ? " 심마의 힘이 내상으로 남았다." : "";
            Notice(won ? $"심마를 꺾고 {MenuScreen.RealmNames[(int)h.Realm]}에 올랐다!{scar}{state.MountainGoal}" : "심마에게 무너져 내상을 입었다.");
            SetMode(Mode.Region);
        }

        float BreakthroughBonus => state.Sect != null ? 0.1f : 0; // 문파 수련동

        internal void CloseMenuForTest() => CloseMenu();
        internal void UseMountainExitForTest() => Descend();
        internal void ToMapForTest() => SetMode(Mode.Region);

        internal void Goto(string nodeId)
        {
            if (state.Location == world.overworld) RememberSpot(region.Cell);
            state.Location = nodeId;
            Arrive();
        }
        internal void ShowCreateForTest()
        {
            SetMode(Mode.Create);
            ShowCreate();
        }

        internal void OpenMenu()
        {
            if (menu != null) return;
            var node = world.Node(state.Location);
            var party = Party.Select(c => c.name).ToArray();
            menu = new MenuScreen(state, party, node.kind == NodeKind.Town, BreakthroughBonus,
                (t, art) => Queue(() => Train(t, art)),
                art => Queue(() => OpenForm(art)),
                () => Queue(() => { state.Hero.Rest(atSect: true); Reply("푹 쉬었다."); }),
                () => Queue(StartClosedDoor),
                () => Queue(() => { Save(); Reply("저장했다."); }),
                () => Queue(Load),
                () => Queue(CloseMenu));
            root.Add(menu.Root);
        }

        // 메뉴를 거치지 않고 개발 패널에서 바로 열 수도 있다.
        void HideMenu(bool hide)
        {
            if (menu != null) menu.Root.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void Reply(string text)
        {
            if (menu == null)
            {
                if (text != "") Notice(text);
                return;
            }
            menu.Message = text;
            menu.Refresh();
        }

        void CloseMenu()
        {
            menu?.Root.RemoveFromHierarchy();
            menu = null;
            if (state != null && mode == Mode.Region) SetMode(Mode.Region); // HUD를 새로 고친다
        }

        internal void OpenCirculation(StatKind? site = null)
        {
            HideMenu(true);
            Circulation = new CirculationScreen(state, rng, message => Queue(() =>
            {
                Circulation.Root.RemoveFromHierarchy();
                Circulation = null;
                state.TrainingStrain();
                HideMenu(false);
                Reply(message);
                if (site != null) talk?.Resume();
            }), site);
            root.Add(Circulation.Root);
        }

        // 산의 출구: 목표를 이뤘으면 내려간다.
        void Descend()
        {
            if (!state.CanDescend)
            {
                Notice("절벽이 까마득하다. 이류에 올라야 넘을 수 있다.");
                region.StepBack();
                return;
            }
            int days = state.MountainDay;
            state.LeaveMountain(world.start);
            Arrive();
            Notice($"{days}일 만에 절벽을 넘어 세상으로 돌아왔다. 백로진의 밥 냄새가 코를 찌른다.");
        }

        // 굶거나 얼어붙으면 약초꾼에게 업혀 내려온다.
        void ForceDescent()
        {
            state.Hero.InnerInjured = true;
            state.Hero.Fatigue = state.Hero.MaxFatigue / 2;
            state.LeaveMountain(world.start);
            Arrive();
            Notice("쓰러진 것을 약초꾼이 찾아내 업고 나왔다. 백로진에서 눈을 떴다. (내상)");
        }

        internal void OpenForm(MartialArt art)
        {
            HideMenu(true);
            Form = new FormScreen(state.Hero, art, message => Queue(() =>
            {
                Form.Root.RemoveFromHierarchy();
                Form = null;
                HideMenu(false);
                Reply(message);
            }));
            root.Add(Form.Root);
        }

        void Train(Training t, MartialArt art)
        {
            if (t == Training.Tonap)
            {
                OpenCirculation();
                return;
            }
            var h = state.Hero;
            bool tired = h.Fatigue >= h.MaxFatigue * 8 / 10;
            h.Train(t, art);
            state.TrainingStrain();
            Sound.Play("Move1");
            Reply(tired ? "지쳐서 효과가 반감됐다." : "수련을 마쳤다.");
        }
    }
}
