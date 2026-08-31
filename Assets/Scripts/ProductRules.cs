using UnityEngine;

namespace AliceMirrorfall
{
    /// <summary>Pure, testable rules shared by the game loop and release tests.</summary>
    public static class ProductRules
    {
        public static int ScoreWithChain(int baseScore, int chain)
        {
            return Mathf.FloorToInt(baseScore * (1f + Mathf.Floor(Mathf.Max(0, chain) / 10f) * .15f));
        }

        public static int ClampStage(int requestedStage, int unlockedStage)
        {
            return Mathf.Clamp(requestedStage, 1, Mathf.Clamp(unlockedStage, 1, GameCatalog.StageCount));
        }

        public static int CycleOption(int currentValue, int optionCount)
        {
            if (optionCount <= 0) return 0;
            return (Mathf.Clamp(currentValue, 0, optionCount - 1) + 1) % optionCount;
        }

        public static int CycleStage(int currentStage, int unlockedStage)
        {
            int highestAvailableStage = Mathf.Clamp(unlockedStage, 1, GameCatalog.StageCount);
            int normalizedStage = Mathf.Clamp(currentStage, 1, highestAvailableStage);
            return normalizedStage >= highestAvailableStage ? 1 : normalizedStage + 1;
        }

        public static float DifficultySpeed(int difficulty)
        {
            return .86f + Mathf.Clamp(difficulty, 0, GameCatalog.DifficultyCount - 1) * .12f;
        }

        public static string SanitizeInitials(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "ALI";
            string value = raw.Trim().ToUpperInvariant();
            return value.Length > 3 ? value.Substring(0, 3) : value;
        }

        public static int NextExtendThreshold(int currentThreshold)
        {
            return Mathf.Max(100000, currentThreshold + 100000);
        }
    }
}
