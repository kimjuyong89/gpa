using System;
using UnityEngine;

namespace GPA.Combat
{
    public enum ArtKind { Outer, Movement, Mind, Inner } // 외공, 경공, 심법, 내공
    public enum Affinity { None, Gang, Yu, Kwae }        // 강(剛), 유(柔), 쾌(快)
    public enum StatusKind { None, Bleed, Poison, Pressure, InnerInjury, QiDeviation } // 출혈, 중독, 점혈, 내상, 주화입마

    // 무공 하나의 데이터. 종류에 따라 쓰는 필드가 다르다.
    [CreateAssetMenu(menuName = "GPA/무공")]
    public class MartialArt : ScriptableObject
    {
        public string displayName;
        public ArtKind kind;
        public int innerCost;
        [Tooltip("사용 후 쓸 수 없는 자기 턴 수 (비전 초식)")]
        public int cooldown;

        [Header("외공")]
        public Affinity affinity;
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
        public int evasionBuff;
        public int buffTurns;
        [Tooltip("회피하면 반격")]
        public bool counter;

        [Header("심법·내공 (패시브)")]
        public int maxInnerBonus;
        [Tooltip("운기조식 회복량 추가 (%p)")]
        public int meditateBonus;
        [Tooltip("걸릴 확률이 절반이 된다")]
        public StatusKind[] resists = Array.Empty<StatusKind>();
    }
}
