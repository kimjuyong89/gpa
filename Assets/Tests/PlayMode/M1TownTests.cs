using System.Collections;
using System.IO;
using GPA.Combat;
using GPA.Growth;
using GPA.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GPA.Tests
{
    // 실제 M1Town 씬을 Play 모드로 띄워 생성 → 지역맵 → 대화 → 전투 → 이동 → 저장·불러오기를 돌린다.
    // 대화창·전투창의 OnGUI는 배치 모드에서 그려지지 않으므로 그 흐름 뒤의 상태만 본다.
    public class M1TownTests
    {
        const string SaveFile = "save_test.json";
        TownView town;

        static IEnumerator WaitUntilStopped(GPA.World.RegionView r)
        {
            yield return null;
            while (r.Moving) yield return null;
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("M1Town");
            town = Object.FindFirstObjectByType<TownView>();
            town.saveFile = SaveFile;
            var stats = CharacterCreation.NewStats();
            stats.Outer += 5;
            stats.Light += 5;
            stats.Con += 5;
            town.NewGame(CharacterCreation.Create("유랑객", 1, stats, town.startingArts));
            yield return null;
        }

        [TearDown]
        public void DeleteSave() => File.Delete(Path.Combine(Application.persistentDataPath, SaveFile));

        [UnityTest]
        public IEnumerator NewGameStartsWalkingInBaekrojin()
        {
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.AreEqual("백로진", town.state.Location);
            Assert.IsTrue(town.region.gameObject.activeSelf);
            yield break;
        }

        [UnityTest]
        public IEnumerator WalkingMovesAndStopsAtObstacles()
        {
            var r = town.region;
            var start = r.Cell;
            r.Move(3); // 오른쪽 (길)
            yield return WaitUntilStopped(r);
            Assert.AreEqual(start + Vector2Int.right, r.Cell);

            for (int i = 0; i < 25; i++) // 나루터로 곧장 내려간다
            {
                r.Move(0);
                yield return WaitUntilStopped(r);
            }
            Assert.AreEqual(new Vector2Int(13, 17), r.Cell, "나루터 끝의 연락책에 막혀 멈춘다");
        }

        [UnityTest]
        public IEnumerator TalkingToNpcOpensDialogueAndReturns()
        {
            var r = town.region;
            r.Move(3); // 객잔 앞 점소이 아래 칸으로
            yield return WaitUntilStopped(r);
            r.Move(1); // 위를 보면 점소이
            yield return null;
            r.Interact();
            yield return null;
            yield return null;
            Assert.AreEqual(TownView.Mode.Talk, town.mode);
            Assert.IsTrue(town.region.Paused, "대화 중에는 걷지 못한다");
        }

        // SNAP_DIR 환경 변수가 있으면 화면별 캡처를 PNG로 남긴다.
        [UnityTest, Timeout(60000)]
        public IEnumerator Screenshots()
        {
            string dir = System.Environment.GetEnvironmentVariable("SNAP_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("SNAP_DIR 없음");

            var rt = new RenderTexture(1280, 720, 24);
            town.panelSettings.targetTexture = rt;
            town.panelSettings.clearColor = false;
            Camera.main.targetTexture = rt;

            IEnumerator Snap(string name, float wait = 0.3f)
            {
                yield return new WaitForSecondsRealtime(wait);
                yield return null; // WaitForEndOfFrame은 배치 모드에서 끝나지 않는다
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }

            yield return Snap("region");
            town.Talk("객잔_점소이");
            yield return Snap("talk", 1f);
            town.ToMapForTest();
            town.OpenMenu();
            yield return Snap("menu");
            town.CloseMenuForTest();
            town.Goto("강호");
            yield return Snap("world");
            town.ShowCreateForTest();
            yield return Snap("create");
            town.StartBattle("갈대밭_도적", false);
            yield return Snap("battle", 0.8f);
            town.StopAllCoroutines();
            town.EndBattle(Outcome.Victory);
            town.state.Set("동료.소육", 1);
            town.StartBattle("흑사방_졸개들", false);
            yield return Snap("battle_party", 0.8f);
            town.StopAllCoroutines();
            town.EndBattle(Outcome.Victory);

            foreach (var art in town.AllArts)
                if (!town.state.Hero.Arts.Contains(art)) town.state.Hero.Arts.Add(art);
            foreach (var name in new[] { "석두", "운학" }) town.state.Recruit(name);
            town.StartBattle("흑사방_패거리", false);
            yield return Snap("battle_full", 0.8f);
            town.StopAllCoroutines();
            town.EndBattle(Outcome.Victory);
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "진형").First());
            yield return Snap("formation");
            town.CloseMenuForTest();
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "수련").First());
            yield return Snap("training");
            town.CloseMenuForTest();

            foreach (var name in new[] { "소육", "석두", "운학" }) town.state.Dismiss(name);
            town.StartBattle("갈대밭_도적", false);
            Button Command(string start) => town.UiRoot.Query<Button>(className: "art-btn").Where(b => b.text.StartsWith(start)).First();
            for (float t = 0; t < 10 && Command("무공") == null; t += Time.unscaledDeltaTime) yield return null;
            Press(Command("무공"));
            yield return Snap("battle_arts");
            Press(Command("뒤로"));
            yield return null;
            Press(Command("경공"));
            yield return Snap("battle_steps");
            town.StopAllCoroutines();
            town.EndBattle(Outcome.Victory);

            var h = town.state.Hero;
            foreach (var name in new[] { "철검", "베옷", "해독 향낭", "회기단" }) h.Bag.Add(ItemData.Find(name));
            h.Equip(ItemData.Find("철검"));
            h.Equip(ItemData.Find("베옷"));
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "소지품").First());
            yield return Snap("bag");
            town.CloseMenuForTest();
            h.Bag.Add(ItemData.Find("대환단"));
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "소지품").First());
            yield return null;
            Press(town.UiRoot.Query<Button>(className: "item-btn").Where(b => b.text == "사용").First());
            yield return Snap("elixir");
            town.CloseMenuForTest();
            town.StartClosedDoor();
            yield return Snap("demon", 2.5f);
            town.EndClosedDoor(Outcome.Fled);
            town.ToggleDev();
            yield return Snap("dev");
            town.ToggleDev();
            town.state.EnterMountain();
            town.Goto("무명산");
            yield return Snap("mountain");
            town.Talk("산_움막");
            yield return Snap("camp", 1f);
            town.ToMapForTest();
            town.state.LeaveMountain("백로진");
            town.Goto("백로진");
            town.Talk("장터_상인");
            town.OpenShop("백로진");
            yield return Snap("shop");
            town.StopAllCoroutines();

            town.OpenMenu();
            var formArt = h.Arts.Find(a => a.kind == GPA.Combat.ArtKind.Outer);
            h.GainMastery(formArt, 100000);
            town.OpenForm(formArt);
            town.Form.Begin();
            while (town.Form.Sim.Clock < 6)
            {
                foreach (var n in town.Form.Sim.Notes)
                    if (n.Hit == Hit.None && n.Time <= town.Form.Sim.Clock) town.Form.Press(n.Lane);
                yield return null;
            }
            yield return Snap("form", 0);
            town.Form.Root.RemoveFromHierarchy();
            town.Form = null;

            town.OpenCirculation();
            yield return Snap("circulation_choice");
            town.Circulation.Begin(Effort.Earnest);
            for (float t = 0; t < 6; t += Time.deltaTime)
            {
                town.Circulation.PointerHeld = town.Circulation.Sim.Speed < town.Circulation.Sim.WantNow;
                yield return null;
            }
            yield return Snap("circulation", 0);
        }

        // 수련 탭의 일주천을 눌러 한 바퀴 돌리면 내공이 쌓이고 메뉴로 돌아온다. 다음부터는 자동 수련을 고를 수 있다.
        [UnityTest, Timeout(60000)]
        public IEnumerator CirculationFromTheMenuGivesInnerPower()
        {
            var h = town.state.Hero;
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "수련").First());
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("일주천")).First());
            yield return null;
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("안정")).First());

            Time.timeScale = 4;
            var screen = town.Circulation;
            for (float t = 0; t < 30 && screen.Root.Q(className: "result") == null; t += Time.unscaledDeltaTime)
            {
                screen.PointerHeld = screen.Sim.Speed < screen.Sim.WantNow;
                yield return null;
            }
            Time.timeScale = 1;
            Assert.AreEqual(CirculationEnd.Complete, screen.Sim.Result);
            Press(screen.Root.Q(className: "result").Q<Button>());
            yield return null;
            yield return null;
            Assert.IsNull(town.Circulation);
            Assert.Greater(h.InnerPower, 0);
            Assert.AreEqual(20, h.Fatigue);

            Press(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("일주천")).First());
            yield return null;
            yield return null;
            Assert.IsNotNull(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("자동 수련")).First());
            town.CloseMenuForTest();
        }

        Character Hermit()
        {
            var stats = CharacterCreation.NewStats();
            stats.Outer += 5;
            stats.Light += 5;
            stats.Con += 5;
            return CharacterCreation.Create("은자", 0, stats, town.startingArts);
        }

        Character Swift()
        {
            var stats = CharacterCreation.NewStats();
            stats.Light += 7;
            stats.Luck += 4;
            stats.Con += 4;
            return CharacterCreation.Create("풍운", 0, stats, town.startingArts);
        }

        Button Flee() => town.UiRoot.Query<Button>(className: "art-btn").Where(b => b.text.StartsWith("도주")).First();

        IEnumerator WaitForFlee()
        {
            Time.timeScale = 8;
            for (float t = 0; t < 20 && (Flee() == null || !Flee().enabledSelf); t += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1;
        }

        IEnumerator FleeAndEnd()
        {
            Press(Flee());
            Time.timeScale = 8;
            for (float t = 0; t < 20 && town.mode == TownView.Mode.Fight; t += Time.unscaledDeltaTime)
            {
                var result = town.UiRoot.Q(className: "result");
                if (result != null) Press(result.Q<Button>());
                yield return null;
            }
            Time.timeScale = 1;
        }

        // 기연과 경공이 되면 첫 전투에서 달아나다 산으로 떨어진다.
        [UnityTest, Timeout(60000)]
        public IEnumerator FirstFleeWithLuckAndSpeedFallsIntoTheMountain()
        {
            town.NewGame(Swift());
            yield return null;
            town.StartBattle("갈대밭_도적", false);
            yield return WaitForFlee();
            yield return FleeAndEnd();
            Assert.AreEqual(TownView.Mode.Talk, town.mode, "추락 장면이 나온다");
            yield return Finish(town);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.AreEqual("무명산", town.state.Location);
            Assert.IsTrue(town.state.InMountain);
        }

        // 처음 전투가 대화 전투여도 조건이 되면 도주가 열린다.
        [UnityTest, Timeout(60000)]
        public IEnumerator TalkBattleOffersTheFleeOnlyWhenEligible()
        {
            town.NewGame(Swift());
            yield return null;
            town.StartBattle("나루터_창고지기", true);
            yield return WaitForFlee();
            Assert.IsTrue(Flee().enabledSelf);
            town.StopAllCoroutines();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SecondBattleFleeIsOrdinary()
        {
            town.NewGame(Swift());
            yield return null;
            town.state.MarkFirstBattle();
            town.StartBattle("갈대밭_도적", false);
            town.EndBattle(Outcome.Fled);
            yield return null;
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.AreEqual("백로진", town.state.Location);
        }

        static IEnumerator Finish(TownView town)
        {
            for (int i = 0; i < 600 && town.mode == TownView.Mode.Talk; i++)
            {
                town.TalkBox?.Choose(0);
                town.TalkBox?.Advance();
                yield return null;
            }
        }

        // 산에서 시작해 나물을 캐고, 움막에서 먹고 잔다.
        [UnityTest, Timeout(60000)]
        public IEnumerator MountainRouteGathersEatsAndSleeps()
        {
            town.NewGame(Hermit());
            town.state.EnterMountain();
            town.Goto("무명산");
            yield return null;
            var s = town.state;
            Assert.AreEqual("무명산", s.Location);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            int food = s.Food;

            town.Talk("산_나물1");
            yield return Finish(town);
            Assert.Greater(s.Food, food);
            Assert.Greater(s.Hero.Fatigue, 0);

            int after = s.Food;
            town.Talk("산_나물1");
            yield return Finish(town);
            Assert.AreEqual(after, s.Food, "같은 곳은 하루 한 번");

            town.Talk("산_움막");
            yield return Finish(town); // 첫 선택지는 밥 짓기다. 식량이 떨어질 때까지 먹고 잠든다
            Assert.AreEqual(0, s.Food);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MountainExitWaitsForTheGoal()
        {
            town.NewGame(Hermit());
            town.state.EnterMountain();
            town.Goto("무명산");
            yield return null;
            var s = town.state;
            town.UseMountainExitForTest();
            yield return null;
            Assert.AreEqual("무명산", s.Location, "이류 전에는 못 내려간다");

            s.Hero.Realm = Realm.Iryu;
            town.UseMountainExitForTest();
            yield return null;
            Assert.AreEqual("백로진", s.Location);
            Assert.IsFalse(s.InMountain);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator StarvingCarriesTheHeroDownInjured()
        {
            town.NewGame(Hermit());
            town.state.EnterMountain();
            town.Goto("무명산");
            yield return null;
            var s = town.state;
            s.Set("산중.허기", 0);
            yield return null;
            yield return null;
            Assert.AreEqual("백로진", s.Location);
            Assert.IsTrue(s.Hero.InnerInjured);
            Assert.IsFalse(s.InMountain);
        }

        // 바위 수련터는 일주천을 열고, 끝나면 대사로 돌아온다.
        [UnityTest, Timeout(60000)]
        public IEnumerator TrainingSiteOpensCirculationAndResumes()
        {
            town.NewGame(Hermit());
            town.state.EnterMountain();
            town.Goto("무명산");
            yield return null;
            town.Talk("산_바위");
            for (int i = 0; i < 300 && town.Circulation == null; i++)
            {
                town.TalkBox.Choose(0);
                town.TalkBox.Advance();
                yield return null;
            }
            Assert.IsNotNull(town.Circulation, "수련터가 일주천을 연다");
            Press(town.Circulation.Root.Query<Button>().Where(b => b.text == "그만둔다").First());
            yield return null;
            yield return null;
            Assert.IsNull(town.Circulation);
            yield return Finish(town);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
        }

        // 진형 탭에서 칸을 옮기면 전투에서 그 칸에 선다.
        [UnityTest, Timeout(60000)]
        public IEnumerator FormationTabPlacesThePartyInBattle()
        {
            town.state.Recruit("소육");
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "진형").First());
            yield return null;
            Press(town.UiRoot.Query<Button>(className: "grid-cell").Where(b => b.text == "소육").First());
            yield return null;
            Press(town.UiRoot.Query<Button>(className: "grid-cell").Where(b => b.text == "빈 칸").First());
            yield return null;
            int cell = town.state.Formation(new[] { "소육" })["소육"];
            Assert.AreNotEqual(4, cell, "기본 칸에서 옮겼다");

            Press(town.UiRoot.Query<Button>().Where(b => b.text == "소육 떠나보내기").First());
            yield return null;
            Assert.AreEqual(0, town.state.CompanionCount);
            town.CloseMenuForTest();
        }

        // 4명이 적 6명과 끝까지 싸운다. 자리 겹침이나 대상 고르기에서 멈추지 않아야 한다.
        [UnityTest, Timeout(120000)]
        public IEnumerator FullGridBattlePlaysToTheEnd()
        {
            foreach (var name in new[] { "소육", "석두", "연아" }) Assert.IsTrue(town.state.Recruit(name));
            Time.timeScale = 8;
            town.StartBattle("흑사방_패거리", false);
            for (float t = 0; t < 110 && town.mode == TownView.Mode.Fight; t += Time.unscaledDeltaTime)
            {
                var result = town.UiRoot.Q(className: "result");
                var target = town.UiRoot.Q(className: "unit-target");
                if (result != null) Press(result.Q<Button>());
                else if (target != null)
                {
                    using var click = ClickEvent.GetPooled();
                    click.target = target;
                    target.SendEvent(click);
                }
                else
                {
                    var button = town.UiRoot.Query<Button>(className: "art-btn").Where(x => x.enabledSelf).First();
                    if (button != null) Press(button);
                }
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Time.timeScale = 1;
            Assert.AreEqual(TownView.Mode.Region, town.mode);
        }

        // 수련 탭에서 운용 심법을 바꾸면 전투에 그 심법으로 들어간다.
        [UnityTest, Timeout(60000)]
        public IEnumerator TrainingTabSwitchesTheActiveMind()
        {
            var h = town.state.Hero;
            var pure = System.Linq.Enumerable.First(town.AllArts, a => a.displayName == "청심결");
            h.Arts.Add(pure);
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "수련").First());
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("청심결")).First());
            yield return null;
            Assert.AreSame(pure, h.ActiveMind);
            Assert.AreSame(pure, h.ToCombatant().Mind);
            town.CloseMenuForTest();
        }

        // 경험치가 찬 무공은 수련 탭에 연마가 뜨고, 초식을 다 치면 6성에 오른다.
        [UnityTest, Timeout(60000)]
        public IEnumerator FormPracticeOpensTheGate()
        {
            var h = town.state.Hero;
            var art = h.Arts.Find(a => a.kind == GPA.Combat.ArtKind.Outer);
            h.GainMastery(art, 100000);
            Assert.AreEqual(5, h.Mastery(art));
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "수련").First());
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text.EndsWith("6성 연마")).First());
            yield return null;
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "연마한다").First());

            Time.timeScale = 4;
            var screen = town.Form;
            for (float t = 0; t < 30 && screen.Root.Q(className: "result") == null; t += Time.unscaledDeltaTime)
            {
                foreach (var n in screen.Sim.Notes)
                    if (n.Hit == Hit.None && n.Time <= screen.Sim.Clock) screen.Press(n.Lane);
                yield return null;
            }
            Time.timeScale = 1;
            Assert.IsTrue(screen.Sim.Passed, $"숙달 {screen.Sim.Score}");
            Press(screen.Root.Q(className: "result").Query<Button>().Where(b => b.text == "계속").First());
            yield return null;
            yield return null;
            Assert.IsNull(town.Form);
            Assert.AreEqual(8, h.Mastery(art), "경험치가 충분해서 다음 관문 앞까지 오른다");
            Assert.AreEqual(25, h.Fatigue);
            town.CloseMenuForTest();
        }

        static IEnumerator DevPress(TownView town, string label)
        {
            Press(town.DevRoot.Query<Button>().Where(b => b.text == label).First());
            yield return null;
            yield return null;
        }

        // 개발 패널로 값을 바꾸고 각 화면을 바로 연다.
        [UnityTest, Timeout(60000)]
        public IEnumerator DevPanelReachesEveryFeature()
        {
            var h = town.state.Hero;
            town.ToggleDev();
            Assert.IsNotNull(town.DevRoot);

            yield return DevPress(town, "돈 +1000");
            Assert.AreEqual(1050, h.Money);
            yield return DevPress(town, "장비 전부");
            yield return DevPress(town, "약·영약 전부 5개");
            Assert.AreEqual(5, h.Bag.Count(ItemData.Find("대환단")));
            yield return DevPress(town, "청운검문 직전제자");
            Assert.AreEqual("청운검문", town.state.Sect);
            Assert.IsTrue(town.state.Disciple);

            yield return DevPress(town, "일주천: 무리");
            Assert.IsNull(town.DevRoot, "화면을 열면 패널이 닫힌다");
            Assert.AreEqual(GPA.Growth.Effort.Reckless, town.Circulation.Sim.Effort);
            town.Circulation.Root.RemoveFromHierarchy();
            town.Circulation = null;

            town.ToggleDev();
            yield return DevPress(town, "전진검법 10성");
            Assert.AreEqual(10, town.Form.Sim.Gate);
            town.Form.Root.RemoveFromHierarchy();
            town.Form = null;

            town.ToggleDev();
            yield return DevPress(town, "폐관수련: 심마");
            Assert.AreEqual(TownView.Mode.Fight, town.mode);
            town.ToggleDev();
            yield return DevPress(town, "즉시 승리");
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.AreEqual(Realm.Iryu, h.Realm);

            town.ToggleDev();
            yield return DevPress(town, "강호");
            Assert.AreEqual("강호", town.state.Location);

            town.ToggleDev();
            yield return DevPress(town, "장터 상인");
            Assert.AreEqual(TownView.Mode.Talk, town.mode);
        }

        // 내공을 채우고 폐관수련을 고르면 심마 전투가 열리고, 끝까지 싸우면 지도로 돌아온다.
        [UnityTest, Timeout(120000)]
        public IEnumerator ClosedDoorFightsTheInnerDemon()
        {
            var h = town.state.Hero;
            h.InnerPower = h.InnerPowerToBreak;
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "수련").First());
            yield return null;
            Press(town.UiRoot.Query<Button>().Where(b => b.text.StartsWith("폐관수련")).First());
            yield return null;
            yield return null;
            Assert.AreEqual(TownView.Mode.Fight, town.mode);

            Time.timeScale = 8;
            bool tempted = false;
            for (float t = 0; t < 110 && town.mode == TownView.Mode.Fight; t += Time.unscaledDeltaTime)
            {
                tempted |= town.UiRoot.Query<Button>(className: "art-btn").Where(b => b.text.StartsWith("유혹")).First() != null;
                var result = town.UiRoot.Q(className: "result");
                var button = result != null ? result.Q<Button>() : town.UiRoot.Query<Button>(className: "art-btn").Where(x => x.enabledSelf).First();
                if (button != null) Press(button);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Time.timeScale = 1;
            Assert.IsTrue(tempted, "유혹 버튼이 나온다");
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.IsTrue(h.Realm == Realm.Iryu || (h.InnerInjured && h.InnerPower == h.InnerPowerToBreak * 8 / 10), "돌파하거나 내상을 입는다");
        }

        [UnityTest]
        public IEnumerator DefeatByTheDemonInjuresAndVictoryAscends()
        {
            var h = town.state.Hero;
            h.InnerPower = h.InnerPowerToBreak;
            town.StartClosedDoor();
            town.EndClosedDoor(Outcome.Defeat);
            Assert.AreEqual(Realm.Samryu, h.Realm);
            Assert.IsTrue(h.InnerInjured);
            Assert.AreEqual(80, h.InnerPower);
            yield return null;

            h.InnerInjured = false;
            h.InnerPower = h.InnerPowerToBreak;
            town.StartClosedDoor();
            town.EndClosedDoor(Outcome.Victory);
            Assert.AreEqual(Realm.Iryu, h.Realm);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
        }

        // 상인에게 말을 걸어 상점을 열고, 사고 나면 대화로 돌아가 끝난다.
        [UnityTest, Timeout(60000)]
        public IEnumerator ShopOpensFromDialogueAndResumes()
        {
            var h = town.state.Hero;
            h.Money = 300;
            town.Talk("장터_상인");
            for (int i = 0; i < 300 && town.UiRoot.Q(className: "shop") == null; i++)
            {
                town.TalkBox.Choose(0);
                town.TalkBox.Advance();
                yield return null;
            }
            var shop = town.UiRoot.Q(className: "shop");
            Assert.IsNotNull(shop, "상점이 열린다");

            Press(shop.Query<Button>(className: "item-btn").First());
            yield return null;
            Assert.Less(h.Money, 300);
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "닫기").First());
            for (int i = 0; i < 300 && town.mode == TownView.Mode.Talk; i++)
            {
                town.TalkBox?.Advance();
                yield return null;
            }
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.IsNull(town.UiRoot.Q(className: "shop"));
        }

        // 소지품에서 영약을 먹으면 폐관수련 없이 경지가 오르고 메뉴에 알려 준다.
        [UnityTest, Timeout(60000)]
        public IEnumerator ElixirBreaksThroughFromTheBag()
        {
            var h = town.state.Hero;
            h.Bag.Add(ItemData.Find("대환단"));
            town.OpenMenu();
            Press(town.UiRoot.Query<Button>().Where(b => b.text == "소지품").First());
            yield return null;
            Press(town.UiRoot.Query<Button>(className: "item-btn").Where(b => b.text == "사용").First());
            yield return null;
            Assert.AreEqual(Realm.Iryu, h.Realm);
            Assert.IsNotNull(town.UiRoot.Query<Label>().Where(l => l.text.StartsWith("경지를 돌파했다")).First());
            town.CloseMenuForTest();
        }

        // 전투 메뉴에서 아이템을 골라 쓰면 가방에서 사라지고 내상이 낫는다.
        [UnityTest, Timeout(60000)]
        public IEnumerator BattleItemMenuUsesAPill()
        {
            var h = town.state.Hero;
            var pill = ItemData.Find("청심환");
            h.Bag.Add(pill);
            h.InnerInjured = true;
            Time.timeScale = 8;
            town.StartBattle("갈대밭_도적", false);
            Button Find(string start) => town.UiRoot.Query<Button>(className: "art-btn").Where(b => b.text.StartsWith(start)).First();

            for (float t = 0; t < 30 && Find("아이템") == null; t += Time.unscaledDeltaTime) yield return null;
            Press(Find("아이템"));
            yield return null;
            Press(Find("청심환"));
            for (float t = 0; t < 5 && h.Bag.Count(pill) > 0; t += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1;
            Assert.AreEqual(0, h.Bag.Count(pill));
            town.StopAllCoroutines();
            town.EndBattle(Outcome.Fled);
            Assert.IsFalse(h.InnerInjured, "약이 내상을 고쳤다");
        }

        // 경공을 익히면 달리고 뛰어넘을 수 있고, 강 위 섬의 은둔 노인에게 닿는다.
        [UnityTest, Timeout(60000)]
        public IEnumerator LeapReachesTheIslandHermit()
        {
            Time.timeScale = 6;
            var r = town.region;
            var hero = town.state.Hero;
            var step = new GPA.Combat.MartialArt[] { ScriptableObject.CreateInstance<GPA.Combat.MartialArt>() };
            step[0].kind = GPA.Combat.ArtKind.Movement;

            r.Move(1); // 위를 본다
            yield return WaitUntilStopped(r);
            Assert.IsFalse(r.Leap(), "경공 없이는 뛸 수 없다");

            hero.Arts.Add(step[0]);
            yield return null;

            IEnumerator Walk(int dir, int cells)
            {
                for (int i = 0; i < cells; i++)
                {
                    r.Move(dir);
                    yield return WaitUntilStopped(r);
                }
            }

            yield return Walk(0, 1); // 시작 위치로 되돌아오는 경우 대비: 큰길 안쪽으로
            var from = r.Cell;
            yield return Walk(3, 20 - from.x);
            yield return Walk(0, 16 - r.Cell.y);
            Assert.AreEqual(new Vector2Int(20, 16), r.Cell);

            r.Move(0); // 강 쪽을 본다: 물이라 걷지는 못한다
            yield return WaitUntilStopped(r);
            Assert.AreEqual(new Vector2Int(20, 16), r.Cell);
            Assert.IsTrue(r.Leap());
            yield return WaitUntilStopped(r);
            Assert.AreEqual(new Vector2Int(20, 18), r.Cell, "물을 넘어 섬에 내린다");

            r.Move(3); // 은둔 노인을 본다
            yield return null;
            r.Interact();
            yield return null;
            yield return null;
            Assert.AreEqual(TownView.Mode.Talk, town.mode);
            for (int i = 0; i < 300 && town.mode == TownView.Mode.Talk; i++)
            {
                town.TalkBox?.Advance();
                yield return null;
            }
            Time.timeScale = 1;
            Assert.AreEqual(30, hero.InnerPower, "은둔 노인이 내공을 나눠 준다");
            Assert.AreEqual(1, town.state.Get("기연.은둔노인"));
        }

        static void Press(Button b)
        {
            using var e = NavigationSubmitEvent.GetPooled();
            e.target = b;
            b.SendEvent(e);
        }

        // 실제 전투 화면의 버튼을 눌러 결과창까지 끝낸다. 돌진·타격 연출이 그대로 돌아간다.
        [UnityTest, Timeout(120000)]
        public IEnumerator BattleScreenPlaysToTheEnd()
        {
            Time.timeScale = 8;
            town.StartBattle("나루터_창고지기", false);
            for (float t = 0; t < 110 && town.mode == TownView.Mode.Fight; t += Time.unscaledDeltaTime)
            {
                var result = town.UiRoot.Q(className: "result");
                var button = result != null
                    ? result.Q<Button>()
                    : town.UiRoot.Query<Button>(className: "art-btn").Where(x => x.enabledSelf).First();
                if (button != null) Press(button);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            Time.timeScale = 1;
            Assert.AreEqual(TownView.Mode.Region, town.mode, "결과창의 계속을 누르면 지도로 돌아간다");
        }

        // 나루터에서 맞서면 대화 → 전투 → 대화로 돌아와 끝난다.
        [UnityTest, Timeout(60000)]
        public IEnumerator BattleInsideDialogueReturnsToTheMap()
        {
            town.Talk("나루터_흑사방");
            for (int i = 0; i < 200 && town.mode == TownView.Mode.Talk; i++)
            {
                town.TalkBox.Choose(0);
                town.TalkBox.Advance();
                yield return null;
            }
            Assert.AreEqual(TownView.Mode.Fight, town.mode);

            town.EndBattle(Outcome.Victory);
            Assert.AreEqual(TownView.Mode.Talk, town.mode);
            for (int i = 0; i < 200 && town.mode == TownView.Mode.Talk; i++)
            {
                town.TalkBox?.Advance();
                yield return null;
            }
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.AreEqual(1, town.state.Get("평판.백로진"));
            Assert.AreEqual(60, town.state.Hero.Money, "승리 보상 10냥");
        }

        [UnityTest]
        public IEnumerator EncounterBattlePaysRewardAndReturns()
        {
            town.Goto("강호");
            yield return null;
            int money = town.state.Hero.Money;
            town.StartBattle("갈대밭_도적", false);
            Assert.AreEqual(TownView.Mode.Fight, town.mode);
            Assert.IsFalse(town.region.gameObject.activeSelf, "전투 중에는 지도를 숨긴다");
            town.EndBattle(Outcome.Victory);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
            Assert.IsTrue(town.region.gameObject.activeSelf);
            Assert.AreEqual(money + 20, town.state.Hero.Money);
            Assert.AreEqual(1, town.state.Get("전투.갈대밭_도적"));
        }

        // 마을 밖으로 나와 큰 지도를 걷고, 입구를 밟으면 마을로 들어가며, 다시 나오면 그 자리다.
        [UnityTest, Timeout(60000)]
        public IEnumerator OverworldWalkAndTownPortal()
        {
            town.Goto("강호");
            yield return null;
            var r = town.region;
            Assert.AreEqual(new Vector2Int(23, 12), r.Cell, "처음 나오면 백로진 앞");

            r.Move(0);
            yield return WaitUntilStopped(r);
            Assert.AreEqual(new Vector2Int(23, 13), r.Cell);
            Assert.AreEqual(1, town.state.Get("걸음"));

            r.Move(1);
            yield return WaitUntilStopped(r);
            r.Move(1); // 입구를 밟는다
            yield return WaitUntilStopped(r);
            yield return null;
            yield return null;
            Assert.AreEqual("백로진", town.state.Location);

            town.Goto("강호");
            yield return null;
            Assert.AreEqual(new Vector2Int(23, 12), town.region.Cell, "입구 앞 자리로 돌아온다");
        }

        [UnityTest]
        public IEnumerator LockedGateTurnsYouBack()
        {
            town.Goto("강호");
            yield return null;
            town.EnterPortal("관도 갈림길");
            yield return null;
            Assert.AreEqual("강호", town.state.Location);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SaveAndLoadRestoresPlace()
        {
            town.Goto("강호");
            yield return null;
            town.region.Move(0);
            yield return WaitUntilStopped(town.region);
            town.state.Set("호감.소육", 3);
            town.Save();

            town.Goto("백로진");
            yield return null;
            town.state.Set("호감.소육", 0);
            town.Load();
            yield return null;

            Assert.AreEqual("강호", town.state.Location);
            Assert.AreEqual(new Vector2Int(23, 13), town.region.Cell);
            Assert.AreEqual(3, town.state.Get("호감.소육"));
            Assert.AreEqual("유랑객", town.state.Hero.Name);
            Assert.AreEqual(TownView.Mode.Region, town.mode);
        }
    }
}
