using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>곡×난이도별 최고 점수/최대 콤보 + 전체 최고 기록 (PlayerPrefs).</summary>
    public static class HighScores
    {
        static string Key(string song, int difficulty) => $"hs_{song}_{difficulty}";
        static string ComboKey(string song, int difficulty) => $"hc_{song}_{difficulty}";
        const string GlobalKey = "hs_global";

        public static int GetSong(string song, int difficulty) =>
            PlayerPrefs.GetInt(Key(song, difficulty), 0);

        public static int GetCombo(string song, int difficulty) =>
            PlayerPrefs.GetInt(ComboKey(song, difficulty), 0);

        /// <summary>난이도 무관 곡 최고 기록 — 곡 선택 버블용 (점수, 콤보).</summary>
        public static (int score, int combo) GetSongBest(string song, int difficultyCount = 3)
        {
            int bestScore = 0, bestCombo = 0;
            for (int d = 0; d < difficultyCount; d++)
            {
                bestScore = Mathf.Max(bestScore, GetSong(song, d));
                bestCombo = Mathf.Max(bestCombo, GetCombo(song, d));
            }
            return (bestScore, bestCombo);
        }

        /// <summary>타이틀 HI-SCORE — 모든 곡 통틀어 최고.</summary>
        public static int GetGlobal() => PlayerPrefs.GetInt(GlobalKey, 0);

        /// <summary>기록 제출. 곡 신기록(점수 기준)이면 true.</summary>
        public static bool Submit(string song, int difficulty, int score, int maxCombo = 0)
        {
            bool record = score > GetSong(song, difficulty);
            if (record) PlayerPrefs.SetInt(Key(song, difficulty), score);
            if (maxCombo > GetCombo(song, difficulty)) PlayerPrefs.SetInt(ComboKey(song, difficulty), maxCombo);
            if (score > GetGlobal()) PlayerPrefs.SetInt(GlobalKey, score);
            PlayerPrefs.Save();
            return record;
        }
    }
}
