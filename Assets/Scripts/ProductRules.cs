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
            return Mathf.Clamp(requestedStage, 1, Mathf.Clamp(unlockedStage, 1, 3));
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
