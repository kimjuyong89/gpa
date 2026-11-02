using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;

namespace GPA.Growth
{
    // 캐릭터 생성 규칙. 스탯은 모두 Base에서 시작하고 Points를 나눠 준다. 수치는 임시값.
    public static class CharacterCreation
    {
        public const int Base = 3;
        public const int Points = 15;
        public const int Max = 10;            // 삼류 스탯 상한과 같다
        public const int MaxNameLength = 8;
        public static readonly string[] Looks = { "Boy", "Woman", "FighterWhite", "Monk" }; // 외형 번호별 캐릭터 시트
        public static int AppearanceCount => Looks.Length;

        public static readonly StatKind[] All = (StatKind[])Enum.GetValues(typeof(StatKind));

        public static Stats NewStats() => new() { Outer = Base, Inner = Base, Light = Base, Con = Base, Wis = Base, Virtue = Base, Luck = Base };

        public static int Remaining(Stats s) => Base * All.Length + Points - All.Sum(k => s.Ref(k));
        public static bool CanRaise(Stats s, StatKind k) => Remaining(s) > 0 && s.Ref(k) < Max;
        public static bool CanLower(Stats s, StatKind k) => s.Ref(k) > Base;

        public static string CleanName(string name) => (name ?? "").Trim();

        // 이름은 1~8자, 외형은 목록 안, 스탯은 Base~Max이고 포인트를 다 써야 한다.
        public static bool IsValid(string name, int appearance, Stats s)
        {
            name = CleanName(name);
            return name.Length > 0 && name.Length <= MaxNameLength
                && appearance >= 0 && appearance < AppearanceCount
                && All.All(k => s.Ref(k) >= Base && s.Ref(k) <= Max)
                && Remaining(s) == 0;
        }

        public static Character Create(string name, int appearance, Stats s, IEnumerable<MartialArt> startingArts)
        {
            if (!IsValid(name, appearance, s)) throw new ArgumentException("캐릭터 생성 조건을 만족하지 않는다");
            return new Character(CleanName(name), s, startingArts) { Appearance = appearance };
        }
    }
}
