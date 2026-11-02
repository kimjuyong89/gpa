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
    // M1 게임 흐름의 중심. 화면 모드 전환, 입력, 큐.
    // 나머지는 partial로 나뉜다: Travel(지도·저장), Battle(전투), Menu(수련·정비).
    public partial class TownView : MonoBehaviour
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
        internal VisualElement UiRoot => root;
        internal VisualElement DevRoot; // 개발 패널이 떠 있으면 그 화면
        internal DialogueBox TalkBox => talk;

        Dialogue dialogue;          // 게임 하나에 하나. 대본 진행 상태가 이어진다
        VisualElement root, screen, toast;
        Hud hud;
        DialogueBox talk;
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

        internal void NewGame(Character hero)
        {
            hero.Money = 50;
            hero.Bag.Add(ItemData.Find("금창약"), 2);
            state = new GameState(hero, sects) { Location = world.start };
            dialogue = new Dialogue(story.text, state);
            Arrive();
        }

        internal IEnumerable<UnitSetup> Party => companions.Where(c => state.Get("동료." + c.name) > 0).Take(GameState.MaxCompanions);

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
                if (hud != null)
                {
                    hud.Refresh();
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
            hud?.Root.RemoveFromHierarchy();
            hud = null;
            if (m != Mode.Region) return;
            Show(null);
            Sound.Music(world.Node(state.Location).kind == NodeKind.Town ? "Town" : "World");
            ApplyTheme();
            hud = new Hud(state, world, () => Queue(OpenMenu));
            root.Add(hud.Root);
        }

        // 프레임 색은 정사 성향을 따른다. 사파가 정파보다 높으면 청동, 아니면 옥.
        void ApplyTheme() => root.EnableInClassList("theme-bronze", state != null && state.Get("사파") > state.Get("정파"));

        internal void CloseMenuForTest() => CloseMenu();
        internal void UseMountainExitForTest() => Descend();
        internal void ToMapForTest() => SetMode(Mode.Region);
        internal void ShowCreateForTest()
        {
            SetMode(Mode.Create);
            ShowCreate();
        }
    }
}
