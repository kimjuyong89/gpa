using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPA.UI
{
    // 전투 화면. Battle이 쌓은 이벤트를 한 동작씩 재생한다.
    // 아군은 메뉴로 조작하고, 적은 잠시 뜸을 들인 뒤 자동으로 행동한다.
    public class BattleScreen
    {
        const int FacingRight = 3, FacingLeft = 2;

        class Unit
        {
            public Combatant C;
            public VisualElement Box, Sprite, HpFill, InnerFill;
            public Label Status;
            public Sprite[] Idle;  // 픽셀 캐릭터의 숨쉬기 프레임. 입상이면 null
            public float Facing = 1;
            public int ShownHp;
        }

        public readonly VisualElement Root = Ui.Box("fill", "dark-bg");
        readonly Battle battle;
        readonly bool canFlee;
        readonly MonoBehaviour host;
        readonly Action<Outcome> onEnd;
        readonly List<Unit> units = new();
        readonly VisualElement stage = Ui.Box("stage"), timeline = Ui.Box("timeline"), menu = Ui.Box("command-menu");
        readonly Label prompt = Ui.Text("", "heading"), logLine = Ui.Text("", "small", "wrap");
        readonly List<string> log = new();
        readonly Sprite[] slash = Ui.Strip("FX/SpriteSheetSlash01", 5);
        int played;
        bool acted;

        public BattleScreen(Battle battle, bool canFlee, MonoBehaviour host, Action<Outcome> onEnd)
        {
            this.battle = battle;
            this.canFlee = canFlee;
            this.host = host;
            this.onEnd = onEnd;

            Root.Add(stage);
            if (battle.Tempting) stage.style.unityBackgroundImageTintColor = new Color(0.62f, 0.55f, 0.55f); // 마음속이다
            stage.Add(timeline);
            foreach (var c in battle.Allies.Concat(battle.Enemies)) stage.Add(MakeUnit(c));
            Place();

            var bottom = Ui.Box("command");
            var left = Ui.Box("command-log", "panel");
            left.Add(logLine);
            bottom.Add(left);
            var right = Ui.Box("panel", "grow");
            right.Add(prompt);
            var scroll = new ScrollView();
            scroll.AddToClassList("grow");
            scroll.Add(menu);
            right.Add(scroll);
            bottom.Add(right);
            Root.Add(bottom);

            // 숨쉬기: 입상은 살짝 늘었다 줄고, 픽셀은 프레임을 바꾼다.
            Root.schedule.Execute(() =>
            {
                int f = (int)(Time.time * 2.5f) % 2;
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u.C.IsDown) continue;
                    if (u.Idle != null) u.Sprite.style.backgroundImage = new StyleBackground(u.Idle[f]);
                    else u.Sprite.style.scale = new Scale(new Vector2(u.Facing, 1 + 0.012f * Mathf.Sin(Time.time * 2.2f + i)));
                }
            }).Every(50);

            host.StartCoroutine(Run());
        }

        VisualElement MakeUnit(Combatant c)
        {
            var figure = Ui.Body(FigureOf(c));
            var u = new Unit
            {
                C = c,
                Box = Ui.Box("unit"),
                Sprite = Ui.Box(figure != null ? "unit-body" : "unit-sprite"),
                Status = Ui.Text("", "unit-status"),
                ShownHp = c.Hp,
            };
            if (figure != null)
            {
                u.Sprite.style.backgroundImage = new StyleBackground(figure);
                u.Facing = c.IsAlly ? 1 : -1; // 적은 뒤집어 아군 쪽을 보게 한다
                u.Sprite.style.scale = new Scale(new Vector2(u.Facing, 1));
            }
            else
            {
                var sheet = Ui.Sheet(c.Sprite) ?? Ui.Sheet("NinjaDark");
                int facing = c.IsAlly ? FacingRight : FacingLeft;
                u.Idle = new[] { Ui.Frame(sheet, facing, 0), Ui.Frame(sheet, facing, 1) };
                u.Sprite.style.backgroundImage = new StyleBackground(u.Idle[0]);
            }
            u.Box.Add(u.Status);
            u.Box.Add(u.Sprite);
            u.Box.Add(Ui.Text(c.Nature != InnerNature.None ? $"{c.Name} ({Combatant.NatureLabel(c.Nature)})" : c.Name, "unit-name"));
            var hp = Ui.Bar("bar-hp");
            var inner = Ui.Bar("bar-inner");
            u.HpFill = hp.fill;
            u.InnerFill = inner.fill;
            u.Box.Add(hp.bar);
            u.Box.Add(inner.bar);
            units.Add(u);
            return u.Box;
        }

        static string FigureOf(Combatant c) => c.Figure ?? c.Name;

        // 진영마다 3x2 격자다. 전열은 가운데 쪽, 후열은 바깥쪽에 서고, 가로 0이 화면 안쪽, 2가 앞이다.
        // 앞줄일수록 크고 아래에 서서 위에 그려진다. 사람이 적으면 크게 보여 준다.
        static readonly float[] LaneScale = { .54f, .63f, .72f }, LaneBottom = { 236, 118, 6 }, LaneShift = { 14, 7, 0 };
        const float FrontX = 20, BackX = 1, MirrorX = 84.4f;

        void Place()
        {
            float zoom = units.Count <= 3 ? 1.3f : units.Count <= 5 ? 1.12f : 1;
            foreach (var u in units.OrderBy(u => u.C.Lane).ThenBy(u => u.C.Row == Row.Front))
            {
                float x = (u.C.Row == Row.Front ? FrontX : BackX) + LaneShift[u.C.Lane];
                float size = LaneScale[u.C.Lane] * zoom;
                u.Box.style.left = Length.Percent(u.C.IsAlly ? x : MirrorX - x);
                u.Box.style.bottom = LaneBottom[u.C.Lane];
                u.Box.style.scale = new Scale(new Vector2(size, size));
                u.Box.BringToFront();
            }
            timeline.BringToFront();
        }

        void Refresh()
        {
            foreach (var u in units)
            {
                Ui.SetBar(u.HpFill, u.ShownHp / (float)u.C.MaxHp);
                Ui.SetBar(u.InnerFill, u.C.MaxInner == 0 ? 0 : u.C.Inner / (float)u.C.MaxInner);
                string st = string.Join(" ", u.C.Statuses.Select(s => Combatant.Label(s.Kind)));
                if (u.C.InnerInjured) st += " 내상";
                if (u.C.Guard > 0) st += $" 방어{u.C.Guard}";
                if (u.C.EvasionBuff > 0) st += $" 회피{u.C.EvasionBuff}";
                u.Status.text = st.Trim();
                u.Box.EnableInClassList("unit-turn", u.C == battle.Current);
                if (u.ShownHp <= 0) u.Box.style.opacity = 0.35f;
            }
            timeline.Clear();
            timeline.Add(Ui.Text("순서 ", "small"));
            foreach (var c in battle.PreviewOrder(8)) timeline.Add(Ui.Image(Ui.Portrait(FigureOf(c), c.Sprite), "face-small"));
            if (timeline.childCount > 1) timeline[1].AddToClassList("face-now");
        }

        Unit Of(Combatant c) => units.First(u => u.C == c);

        IEnumerator Run()
        {
            Refresh();
            yield return new WaitForSeconds(0.4f);
            while (true)
            {
                var c = battle.BeginTurn();
                yield return Play();
                if (c == null) break;
                Refresh();
                if (c.IsAlly)
                {
                    acted = false;
                    ShowCommands();
                    while (!acted) yield return null;
                    menu.Clear();
                    prompt.text = "";
                }
                else
                {
                    prompt.text = $"{c.Name}의 차례";
                    yield return new WaitForSeconds(0.45f);
                    battle.AutoAct();
                }
                yield return Play();
                Place();
                Refresh();
            }
            battle.Finish();
            Refresh();
            ShowResult();
        }

        // 쌓인 이벤트를 차례로 재생한다.
        IEnumerator Play()
        {
            for (; played < battle.Events.Count; played++)
            {
                var e = battle.Events[played];
                Log(e.Text);
                switch (e.Kind)
                {
                    case EventKind.Act when e.Target != null:
                        yield return Lunge(Of(e.Source), Of(e.Target));
                        break;
                    case EventKind.Hit:
                        yield return Hit(e);
                        break;
                    case EventKind.Dot:
                        Float(Of(e.Target), e.Amount.ToString());
                        Of(e.Target).ShownHp -= e.Amount;
                        Sound.Play("Hit2");
                        Refresh();
                        yield return new WaitForSeconds(0.35f);
                        break;
                    case EventKind.Heal:
                        Float(Of(e.Target), "+" + e.Amount, "float-heal");
                        Of(e.Target).ShownHp += e.Amount;
                        Sound.Play("Bonus");
                        Refresh();
                        yield return new WaitForSeconds(0.35f);
                        break;
                    case EventKind.Dodge:
                        Float(Of(e.Target), "회피", "float-info");
                        Sound.Play("Move1");
                        yield return Shake(Of(e.Target).Sprite, 10, 0.15f);
                        break;
                    case EventKind.Status:
                        Float(Of(e.Target), Combatant.Label(e.Status), "float-status");
                        Refresh();
                        yield return new WaitForSeconds(0.3f);
                        break;
                    default:
                        Sound.Play("Move1");
                        yield return new WaitForSeconds(0.25f);
                        break;
                }
            }
        }

        void Log(string text)
        {
            log.Add(text);
            logLine.text = string.Join("\n", log.Skip(Math.Max(0, log.Count - 6)));
        }

        // 공격자가 상대 쪽으로 달려들었다가 돌아온다. 맞는 동작 사이에 돌아오도록 짧게 끝낸다.
        IEnumerator Lunge(Unit a, Unit t)
        {
            Sound.Play("Slash");
            float dir = a.C.IsAlly ? 1 : -1;
            yield return Slide(a.Sprite, 0, 130 * dir, 0.1f);
            host.StartCoroutine(Slide(a.Sprite, 130 * dir, 0, 0.18f));
        }

        static IEnumerator Slide(VisualElement e, float from, float to, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                e.style.translate = new Translate(Mathf.Lerp(from, to, t / time), 0);
                yield return null;
            }
            e.style.translate = new Translate(to, 0);
        }

        IEnumerator Shake(VisualElement e, float power, float time)
        {
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                e.style.translate = new Translate(UnityEngine.Random.Range(-power, power), UnityEngine.Random.Range(-power, power) / 2);
                yield return null;
            }
            e.style.translate = new Translate(0, 0);
        }

        IEnumerator Hit(BattleEvent e)
        {
            var t = Of(e.Target);
            Sound.Play(e.Crit ? "Impact" : "Hit1");
            host.StartCoroutine(SlashFx(t));
            t.Sprite.style.unityBackgroundImageTintColor = new Color(1, 0.35f, 0.35f);
            t.ShownHp -= e.Amount;
            Float(t, e.Crit ? $"{e.Amount}!" : e.Amount.ToString(), e.Crit ? "float-crit" : null);
            Refresh();
            if (e.Crit) host.StartCoroutine(Shake(stage, 12, 0.2f));
            yield return Shake(t.Sprite, 6, 0.16f);
            t.Sprite.style.unityBackgroundImageTintColor = Color.white;
            if (t.ShownHp <= 0) Sound.Play("Impact");
            yield return new WaitForSeconds(0.1f);
        }

        IEnumerator SlashFx(Unit t)
        {
            var fx = Ui.Box("fx");
            fx.pickingMode = PickingMode.Ignore;
            t.Box.Add(fx);
            foreach (var f in slash)
            {
                fx.style.backgroundImage = new StyleBackground(f);
                yield return new WaitForSeconds(0.035f);
            }
            fx.RemoveFromHierarchy();
        }

        // 머리 위로 떠오르며 사라지는 글자.
        void Float(Unit u, string text, string cls = null)
        {
            var l = Ui.Text(text, "float");
            if (cls != null) l.AddToClassList(cls);
            l.pickingMode = PickingMode.Ignore;
            u.Box.Add(l);
            host.StartCoroutine(Rise(l));
        }

        static IEnumerator Rise(VisualElement e)
        {
            const float time = 0.8f;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                e.style.translate = new Translate(0, -46 * t / time);
                e.style.opacity = 1 - Mathf.Pow(t / time, 3);
                yield return null;
            }
            e.RemoveFromHierarchy();
        }

        // 명령은 두 단계다. 무공 / 경공·보법 / 아이템 / 도주를 고르고 그 안에서 고른다.
        void ShowCommands()
        {
            var a = battle.Current;
            prompt.text = $"{a.Name}의 차례";
            menu.Clear();
            menu.Add(Ui.Btn("무공\n공격·운기조식", ShowArts, true, "art-btn"));
            bool steps = a.Arts.Any(x => x.kind == ArtKind.Movement);
            menu.Add(Ui.Btn(steps ? "경공·보법\n방어·회피" : "경공·보법\n배운 것 없음", ShowSteps, steps, "art-btn"));
            int kinds = battle.UsableItems.Count();
            bool skilled = a.Skills != null;
            menu.Add(Ui.Btn(skilled ? "아이템\n약·의술·독술" : kinds > 0 ? $"아이템\n{kinds}종" : "아이템\n쓸 것 없음", ShowItems, skilled || kinds > 0, "art-btn"));
            if (battle.CanSuccumb) menu.Add(Ui.Btn("유혹에 넘어간다\n회복, 주화입마", () => Do(battle.Succumb), true, "art-btn"));
            else menu.Add(Ui.Btn(canFlee ? $"도주\n{battle.FleeChance:P0}" : "도주\n달아날 수 없다", () => Do(battle.Flee), canFlee, "art-btn"));
        }

        void Back() => menu.Add(Ui.Btn("뒤로", ShowCommands, true, "art-btn"));

        // 내력 소모나 남은 대기 턴.
        static string CostNote(Combatant a, MartialArt art)
        {
            if (!a.MeetsSkill(art)) return $"{Combatant.SkillLabel(art.skill)} {art.skillNeed} 필요";
            int cd = a.Cooldown(art);
            return cd > 0 ? $"대기{cd}" : a.Inner < a.CostOf(art) ? "내력 부족!" : $"내력{a.CostOf(art)}";
        }

        void ShowArts()
        {
            var a = battle.Current;
            prompt.text = "무공을 고르세요";
            menu.Clear();
            foreach (var art in a.Arts.Where(x => x.kind == ArtKind.Outer))
            {
                var targets = battle.Enemies.Where(t => battle.CanUse(art, t)).ToList();
                string guard = art.guard > 0 ? $" 방어{art.guard}%" : "";
                menu.Add(Ui.Btn($"{art.displayName}{FitTag(a.FitOf(art))}\n{Affinity(art)}{CostNote(a, art)}{guard}", () =>
                {
                    if (targets.Count == 1) Do(() => battle.UseArt(art, targets[0]));
                    else ChooseTarget(art.displayName, targets, c => battle.UseArt(art, c));
                }, targets.Count > 0, "art-btn"));
            }
            menu.Add(Ui.Btn("운기조식\n내력 회복", () => Do(battle.Meditate), true, "art-btn"));
            Back();
        }

        // 운용 심법과의 궁합을 무공 이름 옆에 보여 준다. 느낌표는 주화입마 위험이다.
        static string FitTag(MindFit fit) => fit switch
        {
            MindFit.Match => $" +{(Combatant.MatchPower - 1) * 100:0}%",
            MindFit.Weak => $" -{(1 - Combatant.WeakPower) * 100:0}%",
            MindFit.Clash => $" -{(1 - Combatant.ClashPower) * 100:0}%!",
            _ => "",
        };

        void ShowSteps()
        {
            var a = battle.Current;
            prompt.text = "경공·보법을 고르세요";
            menu.Clear();
            foreach (var art in a.Arts.Where(x => x.kind == ArtKind.Movement))
                menu.Add(Ui.Btn($"{art.displayName}\n{CostNote(a, art)} · {art.buffTurns}턴\n방어 {art.guard}% 회피 {art.evasionBuff}%",
                    () => Do(() => battle.UseArt(art)), battle.CanUse(art), "art-btn"));
            Back();
        }

        static string Affinity(MartialArt art) => art.affinity switch
        {
            Combat.Affinity.Gang => "강 ",
            Combat.Affinity.Yu => "유 ",
            Combat.Affinity.Kwae => "쾌 ",
            _ => "",
        };

        void ShowItems()
        {
            prompt.text = "아이템을 고르세요";
            menu.Clear();
            foreach (var item in battle.UsableItems)
            {
                var targets = battle.Allies.Where(t => battle.CanUseItem(item, t)).ToList();
                menu.Add(Ui.Btn($"{item.name} ×{battle.Bag.Count(item)}\n{ShopScreen.Describe(item)}", () =>
                {
                    if (targets.Count == 1) Do(() => battle.UseItem(item, targets[0]));
                    else ChooseTarget(item.name, targets, c => battle.UseItem(item, c));
                }, true, "art-btn"));
            }
            if (battle.Current.Skills != null)
            {
                var hurt = battle.Allies.Where(battle.CanFirstAid).ToList();
                var foes = battle.Enemies.Where(battle.CanThrowPoison).ToList();
                menu.Add(Ui.Btn($"응급치료\n내력{Battle.ActionCost} 의료{battle.Current.SkillOf(SkillKind.Medicine)}", () =>
                {
                    if (hurt.Count == 1) Do(() => battle.FirstAid(hurt[0]));
                    else ChooseTarget("응급치료", hurt, c => battle.FirstAid(c));
                }, hurt.Count > 0, "art-btn"));
                menu.Add(Ui.Btn($"독 뿌리기\n내력{Battle.ActionCost} 독술{battle.Current.SkillOf(SkillKind.Poison)}", () =>
                {
                    if (foes.Count == 1) Do(() => battle.ThrowPoison(foes[0]));
                    else ChooseTarget("독 뿌리기", foes, c => battle.ThrowPoison(c));
                }, foes.Count > 0, "art-btn"));
            }
            Back();
        }

        // 대상이 여럿이면 눌러서 고른다.
        void ChooseTarget(string what, List<Combatant> targets, Action<Combatant> act)
        {
            prompt.text = $"{what}: 대상을 고르세요";
            menu.Clear();
            menu.Add(Ui.Btn("취소", () =>
            {
                ClearTargets();
                ShowCommands();
            }, true, "art-btn"));
            foreach (var c in targets)
            {
                var u = Of(c);
                u.Box.AddToClassList("unit-target");
                u.Box.RegisterCallback<ClickEvent>(TargetClicked);
                u.Box.userData = (Action)(() => Do(() => act(c)));
            }
        }

        void TargetClicked(ClickEvent e)
        {
            var box = (VisualElement)e.currentTarget;
            (box.userData as Action)?.Invoke();
        }

        void ClearTargets()
        {
            foreach (var u in units)
            {
                u.Box.RemoveFromClassList("unit-target");
                u.Box.UnregisterCallback<ClickEvent>(TargetClicked);
                u.Box.userData = null;
            }
        }

        void Do(Action action)
        {
            ClearTargets();
            action();
            acted = true;
        }

        void ShowResult()
        {
            var outcome = battle.Outcome;
            Sound.Play(outcome == Outcome.Victory ? "Bonus" : "Cancel");
            var overlay = Ui.Box("result");
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text(outcome switch { Outcome.Victory => "승리", Outcome.Fled => "달아났다", _ => "패배" }, "title", "center"));
            panel.Add(Ui.Btn("계속", () => onEnd(outcome)));
            overlay.Add(panel);
            Root.Add(overlay);
        }
    }
}
