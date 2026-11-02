using System.Linq;
using GPA.Combat;
using GPA.Growth;
using GPA.UI;
using GPA.World;
using UnityEngine.UIElements;

namespace GPA.Prototype
{
    // 수련·정비 메뉴와 그 안에서 여는 미니게임(일주천, 초식 연마).
    public partial class TownView
    {
        MenuScreen menu;
        internal CirculationScreen Circulation;
        internal FormScreen Form;

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
