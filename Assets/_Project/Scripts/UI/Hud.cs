using System;
using GPA.Growth;
using GPA.Narrative;
using GPA.World;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 지도 위에 떠 있는 상태판: 초상, 이름·경지, 지명, 엽전, 피로 막대, 메뉴 버튼, 조작 안내.
    public class Hud
    {
        public readonly VisualElement Root;
        readonly GameState state;
        readonly Label line;
        readonly VisualElement fatigue;

        public Hud(GameState state, WorldMapData world, Action onMenu)
        {
            this.state = state;
            var h = state.Hero;
            Root = Ui.Box("fill");
            Root.pickingMode = PickingMode.Ignore;
            var tone = Ui.Box("fill", "map-tone");
            tone.pickingMode = PickingMode.Ignore;
            Root.Add(tone);

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
            line = Ui.Text("", "hud-line");
            money.Add(line);
            info.Add(money);
            var (bar, fill) = Ui.Bar("bar-fatigue");
            fatigue = fill;
            info.Add(bar);
            box.Add(info);
            Root.Add(box);
            Root.Add(Ui.Btn("수련·정비 (Tab)", onMenu, true, "hud-menu"));

            bool town = world.Node(state.Location).kind == NodeKind.Town;
            string move = h.MovementArt != null ? "   Shift 달리기   F 도약" : "";
            string way = town ? "   왼쪽 길 끝으로 나가면 강호" : "";
            string dev = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            dev = "   F1 개발 패널";
#endif
            var hint = Ui.Text($"방향키/WASD 이동{move}   E/Space 대화{way}{dev}", "hint");
            hint.pickingMode = PickingMode.Ignore;
            Root.Add(hint);
            Refresh();
        }

        // 매 프레임 바뀌는 것만 다시 쓴다.
        public void Refresh()
        {
            var h = state.Hero;
            string tired = h.Fatigue >= h.MaxFatigue ? "  (지쳐서 느리다)" : "";
            line.text = state.InMountain
                ? $"{state.MountainDay}일째   허기 {state.Hunger}   체온 {state.Warmth}\n식량 {state.Food}   장작 {state.Firewood}   피로 {h.Fatigue}/{h.MaxFatigue}{tired}"
                : $"{h.Money}냥   피로 {h.Fatigue}/{h.MaxFatigue}{tired}";
            Ui.SetBar(fatigue, h.Fatigue / (float)h.MaxFatigue);
        }
    }
}
