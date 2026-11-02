using System;
using GPA.Growth;

namespace GPA.Narrative
{
    // 산중 수련: 시작하자마자 산에 들어가 스스로 먹고 입고 자며 수련한다. 수치는 임시값 (docs/13_산중수련.md).
    // 값 키: "산중"(0 안 감, 1 산에 있음, 2 내려옴) "산중.일" "산중.허기" "산중.체온" "산중.식량" "산중.장작" "산중.가죽" "산중.움막" "산중.옷"
    public partial class GameState
    {
        public const string MountainNode = "무명산";
        const int FallLuck = 7, FallLight = 10; // 첫 전투에서 달아나다 산에 떨어지는 조건
        const int FullMeter = 100, MealValue = 30, FireValue = 30, DailyHunger = 30, DailyCold = 30;
        const int ShelterWarmth = 8, ClothesWarmth = 6, MinCold = 4, HungerPerFiveFatigue = 1, TrainingHunger = 8, HealingHunger = 30;

        public bool InMountain => Get("산중") == 1;

        // 기연과 경공이 받쳐 주면 첫 전투에서 달아나다 산으로 굴러떨어진다.
        public bool CanFallIntoMountain
        {
            get
            {
                var s = Hero.GearedStats;
                return Get("산중") == 0 && s.Luck >= FallLuck && s.Light >= FallLight;
            }
        }

        // 첫 전투를 표시한다. 처음이면 참.
        public bool MarkFirstBattle()
        {
            bool first = Get("첫전투") == 0;
            Set("첫전투", 1);
            return first;
        }
        public int MountainDay => Get("산중.일");
        public int Hunger => Get("산중.허기");   // 높을수록 배부르다. 0이면 쓰러진다
        public int Warmth => Get("산중.체온");   // 0이면 쓰러진다
        public int Food => Get("산중.식량");
        public int Firewood => Get("산중.장작");

        public void EnterMountain()
        {
            Set("산중", 1);
            Set("산중.일", 1);
            Set("산중.허기", 70);
            Set("산중.체온", 70);
            Set("산중.식량", 2);
            Set("산중.장작", 2);
            Location = MountainNode;
        }

        // 한계에 닿으면 강제 하산이다.
        public bool Collapsed => InMountain && (Hunger <= 0 || Warmth <= 0);
        public bool CanDescend => Hero.Realm >= Realm.Iryu;

        // 돌파 뒤에 보여 줄 말. 이류면 내려갈 수 있고, 일류면 산에서 오를 곳이 없다 (절정은 깨달음이 있어야 한다).
        public string MountainGoal => !InMountain ? "" : Hero.Realm switch
        {
            Realm.Iryu => " 이제 절벽을 넘을 수 있다. 더 머물면 일류까지 오를 수 있다.",
            Realm.Ilryu => " 이 산에서 더 오를 곳은 없다.",
            _ => "",
        };

        // 몸을 쓴다. 지쳐서 못 하면 거짓. 일할수록 배고프다.
        public bool Spend(int fatigue)
        {
            if (Hero.Fatigue + fatigue > Hero.MaxFatigue) return false;
            Hero.Fatigue += fatigue;
            if (InMountain) Add("산중.허기", -fatigue / 5 * HungerPerFiveFatigue);
            return true;
        }

        public void TrainingStrain()
        {
            if (InMountain) Add("산중.허기", -TrainingHunger);
        }

        public bool Eat()
        {
            if (Food <= 0) return false;
            Add("산중.식량", -1);
            Set("산중.허기", Math.Min(FullMeter, Hunger + MealValue));
            return true;
        }

        public bool MakeFire()
        {
            if (Firewood <= 0) return false;
            Add("산중.장작", -1);
            Set("산중.체온", Math.Min(FullMeter, Warmth + FireValue));
            return true;
        }

        // 하루가 간다. 움막과 옷이 추위를 막아 준다. 배가 차 있어야 내상이 낫는다.
        public void SleepInMountain()
        {
            Add("산중.일", 1);
            Add("산중.허기", -DailyHunger);
            Add("산중.체온", -Math.Max(MinCold, DailyCold - Get("산중.움막") * ShelterWarmth - Get("산중.옷") * ClothesWarmth));
            Hero.Fatigue = 0;
            if (Hunger >= HealingHunger) Hero.InnerInjured = false;
        }

        public void LeaveMountain(string location)
        {
            Set("산중", 2);
            Location = location;
        }
    }
}
