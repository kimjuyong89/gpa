using System;
using GPA.Combat;
using UnityEngine;

namespace GPA.Narrative
{
    public enum CaughtPenalty { Expel, Hunt } // 이중 소속이 들켰을 때: 파문 / 파문 + 추격

    [Serializable]
    public class SectArt
    {
        public MartialArt art;
        [Tooltip("배우는 데 필요한 공헌도. 0이면 입문할 때 바로 배운다")]
        public int contribution;
    }

    [CreateAssetMenu(menuName = "GPA/문파")]
    public class SectData : ScriptableObject
    {
        [Tooltip("대본에서 쓰는 이름. 예: 청운검문")]
        public string id;
        [Tooltip("\"정파\" \"사파\" \"마교\" 중 하나")]
        public string alignment;
        [Tooltip("사부 이름. 호감 키는 \"호감.<사부>\"")]
        public string master;
        public int discipleAffinity = 10;
        public CaughtPenalty onCaught;
        public SectArt[] arts = Array.Empty<SectArt>();
        [Tooltip("직전제자만 배우는 비전 초식")]
        public MartialArt secretArt;
    }
}
