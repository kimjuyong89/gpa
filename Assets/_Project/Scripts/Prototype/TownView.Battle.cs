using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Narrative;
using GPA.UI;

namespace GPA.Prototype
{
    // 전투 진입과 결과 정산. 조우·대화 전투와 폐관수련(심마)이 다르다.
    public partial class TownView
    {
        Combatant heroUnit;
        Action<Outcome> endFight;   // 지금 전투를 끝내는 쪽
        bool fallOnFlee;            // 이번 전투에서 달아나면 산으로 떨어진다
        BattleData battleData;
        bool battleFromTalk;        // 대화 중 전투면 끝난 뒤 대화로 돌아간다

        float BreakthroughBonus => state.Sect != null ? 0.1f : 0; // 문파 수련동

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
    }
}
