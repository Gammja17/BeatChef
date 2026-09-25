using BeatSlash.Rhythm;
using UnityEngine;

namespace BeatSlash.Menu
{
    /// <summary>메인 메뉴에서 고른 곡/비트맵을 게임플레이 씬으로 넘기는 정적 홀더.</summary>
    public static class SongSelection
    {
        public static AudioClip Clip;
        public static Beatmap Map;
        /// <summary>프롬프트 모드: 0=산개(화면 곳곳), 1=클래식(4방향 접근)</summary>
        public static int PromptMode;
        /// <summary>선택한 난이도 인덱스 — 하이스코어 키에 사용.</summary>
        public static int Difficulty;

        /// <summary>요리 메뉴: 재료 풀 필터. ingredients가 null이면 전 재료 등장.
        /// 이름은 KenneyIngredientBuilder가 만드는 프리팹 이름과 일치해야 한다.</summary>
        public static readonly (string label, string key, string[] ingredients)[] Dishes =
        {
            ("셰프 마음대로", null, null),
            ("과일 샐러드", "salad", new[] { "apple", "lemon", "pear", "coconut", "avocado" }),
            ("스테이크", "steak", new[] { "meat-raw", "meat-sausage", "onion", "mushroom", "paprika", "egg" }),
            ("해물탕", "seafood", new[] { "fish", "tomato", "onion", "mushroom", "paprika" }),
        };

        /// <summary>선택된 요리 인덱스 (기본 0 = 셰프 마음대로).</summary>
        public static int Dish;

        public static string DishLabel => Dishes[Mathf.Clamp(Dish, 0, Dishes.Length - 1)].label;
        /// <summary>결과 화면 요리 이미지 파일명 키 (UI/dish_{key}_{rank}). null이면 공용 세트.</summary>
        public static string DishKey => Dishes[Mathf.Clamp(Dish, 0, Dishes.Length - 1)].key;
        public static string[] DishIngredients => Dishes[Mathf.Clamp(Dish, 0, Dishes.Length - 1)].ingredients;

        public static void Clear()
        {
            Clip = null;
            Map = null;
        }
    }
}
