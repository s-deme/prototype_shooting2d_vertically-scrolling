using NUnit.Framework;

namespace AliceMirrorfall.Tests
{
    public sealed class ProductRulesTests
    {
        [Test] public void ChainMultiplierStartsAtOne() => Assert.AreEqual(1000, ProductRules.ScoreWithChain(1000, 9));
        [Test] public void ChainMultiplierIncreasesAtTen() => Assert.AreEqual(1150, ProductRules.ScoreWithChain(1000, 10));
        [Test] public void LockedStageCannotBeSelected() => Assert.AreEqual(1, ProductRules.ClampStage(3, 1));
        [Test] public void InitialsAreNormalized() => Assert.AreEqual("ALI", ProductRules.SanitizeInitials("alice"));
        [Test] public void EmptyInitialsHaveFallback() => Assert.AreEqual("ALI", ProductRules.SanitizeInitials(" "));
        [Test] public void OptionCycleWrapsAtTheLastOption() => Assert.AreEqual(0, ProductRules.CycleOption(3, 4));
        [Test] public void OptionCycleNormalizesInvalidSelection() => Assert.AreEqual(1, ProductRules.CycleOption(-1, 4));
        [Test] public void StageCycleNeverExceedsUnlockedStage() => Assert.AreEqual(1, ProductRules.CycleStage(1, 1));
        [Test] public void DifficultySpeedIsBoundedToSupportedDifficulties() => Assert.That(ProductRules.DifficultySpeed(99), Is.EqualTo(1.22f).Within(.0001f));
    }
}
