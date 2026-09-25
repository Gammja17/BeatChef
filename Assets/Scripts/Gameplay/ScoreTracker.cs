namespace BeatSlash.Gameplay
{
    /// <summary>한 판의 점수/판정 집계. 결과 화면이 읽는다. GameFlow가 판 시작 때 Reset.</summary>
    public static class ScoreTracker
    {
        public static int Score { get; private set; }
        public static int MaxCombo { get; private set; }
        public static int CurrentCombo { get; private set; }
        public static int Perfect { get; private set; }
        public static int Good { get; private set; }
        public static int Miss { get; private set; }

        public static int Total => Perfect + Good + Miss;

        /// <summary>정확도 0~1: Perfect 만점, Good 절반.</summary>
        public static float Accuracy => Total == 0 ? 0f : (Perfect + Good * 0.5f) / Total;

        /// <summary>판정 없는 보너스 점수 (대형 재료 연타 등).</summary>
        public static void AddBonus(int amount) => Score += amount;

        public static void Reset()
        {
            Score = MaxCombo = CurrentCombo = Perfect = Good = Miss = 0;
        }

        public static void Register(Rhythm.JudgeResult result, int gain)
        {
            Score += gain;
            switch (result)
            {
                case Rhythm.JudgeResult.Perfect:
                    Perfect++;
                    CurrentCombo++;
                    break;
                case Rhythm.JudgeResult.Good:
                    Good++;
                    CurrentCombo++;
                    break;
                case Rhythm.JudgeResult.Miss:
                    Miss++;
                    CurrentCombo = 0;
                    break;
            }
            if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;
        }
    }
}
