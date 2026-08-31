namespace AliceMirrorfall
{
    /// <summary>
    /// Central catalogue for the fixed game content used by gameplay and menus.
    /// Keeping these labels and profiles together prevents the selection UI and game loop from drifting apart.
    /// </summary>
    internal static class GameCatalog
    {
        private static readonly string[] StageNames = { "RABBIT HOLE", "MAD TEA GARDEN", "QUEEN'S COURT" };
        private static readonly string[] BossNames = { "WHITE RABBIT", "MAD HATTER", "QUEEN OF HEARTS" };
        private static readonly string[] CharacterNames = { "ALICE", "CHESHIRE", "DORMOUSE" };
        private static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD", "LUNATIC" };
        private static readonly string[] CharacterHints =
        {
            "ALICE：扱いやすい標準ショット",
            "CHESHIRE：広範囲・軽いショット",
            "DORMOUSE：高威力・集中ショット"
        };

        private static readonly string[][] SpellCardNames =
        {
            new[] { "「白兎の急降下」", "「逆さ時計の歯車」", "「穴の底の残像」" },
            new[] { "「狂ったお茶会」", "「砂糖菓子の嵐」", "「帽子屋の悪戯」" },
            new[] { "「紅の女王の行進」", "「時計仕掛けの薔薇園」", "「裁きのハート」" }
        };

        private static readonly PlayerShotProfile[] AliceShotProfiles =
        {
            new PlayerShotProfile(new[] { -7f, 7f }, 1f, .7f),
            new PlayerShotProfile(new[] { -13f, 0f, 13f }, 1f, .7f),
            new PlayerShotProfile(new[] { -18f, -6f, 6f, 18f }, 1f, .7f),
            new PlayerShotProfile(new[] { -24f, -12f, 0f, 12f, 24f }, 1f, .7f)
        };

        private static readonly PlayerShotProfile CheshireLowPowerProfile = new PlayerShotProfile(new[] { -17f, 0f, 17f }, .85f, 1.25f);
        private static readonly PlayerShotProfile CheshireHighPowerProfile = new PlayerShotProfile(new[] { -34f, -17f, 0f, 17f, 34f }, .85f, 1.25f);
        private static readonly PlayerShotProfile DormouseLowPowerProfile = new PlayerShotProfile(new[] { 0f }, 1.45f, .7f);
        private static readonly PlayerShotProfile DormouseHighPowerProfile = new PlayerShotProfile(new[] { -10f, 10f }, 1.45f, .7f);

        public const int StageCount = 3;
        public const int CharacterCount = 3;
        public const int DifficultyCount = 4;
        public const int SpellCardCount = 3;

        public static string StageName(int stage) => StageNames[ClampOneBased(stage, StageCount) - 1];
        public static string BossName(int stage) => BossNames[ClampOneBased(stage, StageCount) - 1];
        public static string CharacterName(int character) => CharacterNames[ClampZeroBased(character, CharacterCount)];
        public static string DifficultyName(int difficulty) => DifficultyNames[ClampZeroBased(difficulty, DifficultyCount)];
        public static string CharacterHint(int character) => CharacterHints[ClampZeroBased(character, CharacterCount)];
        public static string SpellCardName(int stage, int phase) => SpellCardNames[ClampOneBased(stage, StageCount) - 1][ClampZeroBased(phase, SpellCardNames[0].Length)];

        public static PlayerShotProfile GetShotProfile(int character, int powerLevel)
        {
            int normalizedCharacter = ClampZeroBased(character, CharacterCount);
            int normalizedPower = ClampOneBased(powerLevel, 4);

            if (normalizedCharacter == 1)
            {
                return normalizedPower >= 3 ? CheshireHighPowerProfile : CheshireLowPowerProfile;
            }

            if (normalizedCharacter == 2)
            {
                return normalizedPower >= 3 ? DormouseHighPowerProfile : DormouseLowPowerProfile;
            }

            return AliceShotProfiles[normalizedPower - 1];
        }

        private static int ClampOneBased(int value, int count) => value < 1 ? 1 : value > count ? count : value;
        private static int ClampZeroBased(int value, int count) => value < 0 ? 0 : value >= count ? count - 1 : value;
    }

    internal sealed class PlayerShotProfile
    {
        public readonly float[] lanes;
        public readonly float damageMultiplier;
        public readonly float horizontalVelocityMultiplier;

        public PlayerShotProfile(float[] lanes, float damageMultiplier, float horizontalVelocityMultiplier)
        {
            this.lanes = lanes;
            this.damageMultiplier = damageMultiplier;
            this.horizontalVelocityMultiplier = horizontalVelocityMultiplier;
        }
    }
}
