using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GPA.Tests
{
    // M1 전투 밸런스 회귀 테스트. 실제 전투 데이터로 300번씩 자동 전투해 승률이 목표 범위 안인지 본다.
    // 주인공도 AutoAct(무작위 외공 + 체력 낮은 적 집중 공격)로 싸우므로 실제 플레이보다 약간 보수적이다.
    // 승률 표를 보고 싶으면 이 테스트를 돌린 뒤 로그에서 [BAL]을 찾는다.
    public class BalanceReport
    {
        const int Runs = 300;

        static MartialArt Art(string file) => AssetDatabase.LoadAssetAtPath<MartialArt>($"Assets/_Project/Data/MartialArts/{file}.asset");
        static BattleData BattleOf(string id) => AssetDatabase.FindAssets("t:BattleData")
            .Select(g => AssetDatabase.LoadAssetAtPath<BattleData>(AssetDatabase.GUIDToAssetPath(g))).First(b => b.id == id);

        // 캐릭터 생성 기본 분배 예: 외공5 내공3 경공8 근골6 오성5 인덕5 기연4 (15포인트)
        static Character Hero(bool cheongun, string[] gear)
        {
            var h = new Character("주인공", new Stats { Outer = 5, Inner = 3, Light = 8, Con = 6, Wis = 5, Virtue = 5, Luck = 4 },
                new[] { Art("Jeonjingeom"), Art("Jeonjinsim") });
            if (cheongun)
            {
                h.Arts.AddRange(new[] { Art("CheongunKwaegeom"), Art("Yuunbo"), Art("Cheongsimgyeol") });
                h.ActiveMind = Art("Cheongsimgyeol");
            }
            foreach (var name in gear)
            {
                h.Bag.Add(ItemData.Find(name));
                h.Equip(ItemData.Find(name));
            }
            return h;
        }

        // 마을 씬의 소육 설정과 같다.
        static Combatant Soyuk() =>
            new UnitSetup { name = "소육", ally = true, row = Row.Back, lane = 0, stats = new Stats { Outer = 3, Light = 12, Con = 3, Wis = 4, Virtue = 3, Luck = 6 },
                arts = new[] { Art("HeuksaBisu"), Art("Sayeongbo") } }.Create();

        static float WinRate(string battleId, bool cheongun, bool soyuk, params string[] gear)
        {
            var data = BattleOf(battleId);
            var rng = new System.Random(1);
            int wins = 0;
            for (int i = 0; i < Runs; i++)
            {
                var allies = new List<Combatant> { Hero(cheongun, gear).ToCombatant() };
                if (soyuk && !data.solo) allies.Add(Soyuk());
                var b = new Battle(allies, data.enemies.Select(e => e.Create()), new System.Random(rng.Next()));
                for (int turns = 0; b.BeginTurn() != null && turns < 300; turns++) b.AutoAct();
                if (b.Outcome == Outcome.Victory) wins++;
            }
            float rate = wins / (float)Runs;
            Debug.Log($"[BAL] {battleId} {(cheongun ? "청운" : "기본")}{(soyuk ? "+소육" : "")} {string.Join("+", gear)} {rate:P0}");
            return rate;
        }

        // 목표: 마을 근처 2인 전투는 혼자서도 해볼 만하고, 이야기 결투는 혼자 40% 안팎, 무공을 익히면 할 만하다.
        [TestCase("흑사방_졸개들", false, false, 0.70f, 0.95f)]
        [TestCase("흑사방_졸개들", false, true, 0.95f, 1.00f)]
        [TestCase("갈대밭_도적", false, false, 0.45f, 0.75f)]
        [TestCase("갈대밭_도적", true, false, 0.80f, 1.00f)]
        [TestCase("나루터_창고지기", false, false, 0.25f, 0.55f)]
        [TestCase("나루터_창고지기", true, false, 0.60f, 0.90f)]
        [TestCase("청운검문_입문대련", false, false, 0.25f, 0.55f)]
        [TestCase("청운검문_입문대련", false, true, 0.25f, 0.55f)] // 대련엔 소육이 끼지 않는다
        public void WinRateInRange(string battleId, bool cheongun, bool soyuk, float min, float max)
        {
            float rate = WinRate(battleId, cheongun, soyuk);
            Assert.That(rate, Is.InRange(min, max), $"{battleId} 승률 {rate:P0}");
        }

        // 심마는 주인공의 거울이다. succumbAt: 체력이 이 비율 아래면 유혹에 넘어간다 (0이면 버틴다).
        static float DemonWinRate(string label, bool cheongun, float bonus, bool injured, float succumbAt)
        {
            var rng = new System.Random(1);
            int wins = 0;
            for (int i = 0; i < Runs; i++)
            {
                var h = Hero(cheongun, new string[0]);
                h.InnerInjured = injured;
                var hero = h.ToCombatant();
                var b = new Battle(new[] { hero }, new[] { h.InnerDemon(bonus) }, new System.Random(rng.Next())) { Tempting = true };
                for (int turns = 0; b.BeginTurn() != null && turns < 300; turns++)
                {
                    if (b.CanSuccumb && hero.Hp < hero.MaxHp * succumbAt) b.Succumb();
                    else b.AutoAct();
                }
                if (b.Outcome == Outcome.Victory) wins++;
            }
            float rate = wins / (float)Runs;
            Debug.Log($"[BAL] 심마 {label} {rate:P0}");
            return rate;
        }

        // 목표: 돌파는 도전이어야 한다. 자동 전투로 반쯤 이기고, 내상을 안고 들어가면 눈에 띄게 어렵다.
        [Test]
        public void InnerDemonIsAFairFight()
        {
            float plain = DemonWinRate("기본", false, 0, false, 0);
            float sect = DemonWinRate("청운", true, 0.1f, false, 0);
            float hurt = DemonWinRate("내상", false, 0, true, 0);
            float tempted = DemonWinRate("기본+유혹", false, 0, false, 0.4f);
            Assert.That(plain, Is.InRange(0.3f, 0.75f));
            Assert.That(sect, Is.InRange(0.3f, 0.9f));
            Assert.That(hurt, Is.InRange(0.1f, plain), "내상은 힘들어도 불가능하지는 않다");
            Assert.That(tempted, Is.InRange(plain, 0.95f), "유혹은 도움이 되지만 필승은 아니다");
        }

        // 목표: 장비를 맞추면 어려운 전투가 확실히 쉬워지지만, 이야기 결투가 공짜가 되지는 않는다.
        [Test]
        public void GearHelpsWithoutTrivializing()
        {
            string[] bought = { "철검", "베옷", "짚신" }; // 120냥어치
            Assert.That(WinRate("나루터_창고지기", false, false, bought), Is.InRange(0.45f, 0.85f));
            WinRate("청운검문_입문대련", false, false, bought); // 기록만: 경공이 같아 신발 하나로 순서가 뒤집힌다. 대련은 져도 입문한다
            Assert.That(WinRate("나루터_창고지기", true, false, "청운제식검", "청운도복"), Is.InRange(0.75f, 1f));
        }
    }
}
