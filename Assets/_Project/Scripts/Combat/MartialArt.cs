using System;
using UnityEngine;

namespace GPA.Combat
{
    public enum ArtKind { Outer, Movement, Mind, Inner } // 외공, 경공, 심법, 내공
    public enum Affinity { None, Gang, Yu, Kwae }        // 강(剛), 유(柔), 쾌(快)
    public enum SkillKind { None, Sword, Blade, Palm, Finger, Fist, Kick, Hidden, Medicine, Poison, Antidote } // 검술, 도법, 장법, 지법, 권법, 각법, 암기, 의료, 독술, 해독
    public enum InnerNature { None, Yin, Yang, Harmony } // 내공 속성: 없음, 음, 양, 음양조화
    public enum Lineage { Common, Orthodox, Deviant, Universal } // 공용, 정종, 사공, 만류귀종(모든 계열)
    public enum StatusKind { None, Bleed, Poison, Pressure, InnerInjury, QiDeviation } // 출혈, 중독, 점혈, 내상, 주화입마

    // 무공 하나의 데이터. 종류에 따라 쓰는 필드가 다르다.
    [CreateAssetMenu(menuName = "GPA/무공")]
    public class MartialArt : ScriptableObject
    {
        public string displayName;
        [Tooltip("초식 이름. 초식 연마 때 차례로 나온다")]
        public string[] forms = Array.Empty<string>();
        [Tooltip("6성부터 필요한 경험치 배수. 기본 무공처럼 끝이 먼 무공에 쓴다")]
        public int masteryScale = 1;
        [Tooltip("10성을 찍으면 이 무공으로 바뀐다")]
        public MartialArt ascendsTo;
        public ArtKind kind;
        [Tooltip("계열. 운용 심법과 맞아야 제 힘이 난다")]
        public Lineage lineage;
        [Tooltip("이 무공을 쓰면 오르고, 위력·명중을 돕는 기예")]
        public SkillKind skill;
        [Tooltip("비전 초식: 기예가 이만큼 있어야 쓸 수 있다")]
        public int skillNeed;
        public int innerCost;
        [Tooltip("사용 후 쓸 수 없는 자기 턴 수 (비전 초식)")]
        public int cooldown;
        [Tooltip("받는 피해 -%. 외공은 다음 자기 턴까지, 경공은 지속 턴 동안")]
        public int guard;

        [Header("외공")]
        public Affinity affinity;
        [Tooltip("이 종류의 무기를 들면 위력 보너스")]
        public WeaponType weapon;
        public int power;
        public int hits = 1;
        [Tooltip("암기·장풍·의술처럼 후열까지 닿는가")]
        public bool reachesBackRow;
        public StatusKind inflicts;
        [Range(0, 1)] public float inflictChance;
        public int inflictTurns;

        [Header("경공")]
        public bool swapsRow;
        public int speedBuff;
        [Tooltip("회피 확률 +%")]
        public int evasionBuff;
        public int buffTurns;
        [Tooltip("회피하면 반격")]
        public bool counter;

        [Header("심법·내공 (패시브)")]
        [Tooltip("운용하면 이 내공 속성이 된다")]
        public InnerNature nature;
        public int maxInnerBonus;
        [Tooltip("운기조식 회복량 추가 (%p)")]
        public int meditateBonus;
        [Tooltip("걸릴 확률이 절반이 된다")]
        public StatusKind[] resists = Array.Empty<StatusKind>();
        [Tooltip("외공이 맞을 때마다 덧붙는 상태이상")]
        public StatusKind onHit;
        [Range(0, 1)] public float onHitChance;
        public int onHitTurns;
    }
}
