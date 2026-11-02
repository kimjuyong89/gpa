using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.Narrative;
using GPA.UI;
using GPA.World;
using UnityEngine;

namespace GPA.Prototype
{
    // 지도 사이를 오가는 것: 도착, 입구, 조우, 대화, 상점, 산 하산, 저장·불러오기.
    public partial class TownView
    {
        ShopScreen shop;
        string shownLocation;       // 지금 깔린 지도의 위치
        internal string saveFile = "save.json";

        const string WorldX = "강호.x", WorldY = "강호.y"; // 큰 지도에서의 자리

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

        internal void Goto(string nodeId)
        {
            if (state.Location == world.overworld) RememberSpot(region.Cell);
            state.Location = nodeId;
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
    }
}
