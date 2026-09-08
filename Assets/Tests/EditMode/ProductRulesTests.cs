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

        [TestCase("StageName", int.MinValue, "RABBIT HOLE")]
        [TestCase("StageName", 2, "MAD TEA GARDEN")]
        [TestCase("StageName", int.MaxValue, "QUEEN'S COURT")]
        [TestCase("BossName", int.MinValue, "WHITE RABBIT")]
        [TestCase("BossName", 2, "MAD HATTER")]
        [TestCase("BossName", int.MaxValue, "QUEEN OF HEARTS")]
        [TestCase("CharacterName", int.MinValue, "ALICE")]
        [TestCase("CharacterName", 1, "CHESHIRE")]
        [TestCase("CharacterName", int.MaxValue, "DORMOUSE")]
        [TestCase("DifficultyName", int.MinValue, "EASY")]
        [TestCase("DifficultyName", 2, "HARD")]
        [TestCase("DifficultyName", int.MaxValue, "LUNATIC")]
        [TestCase("CharacterHint", int.MinValue, "ALICE：扱いやすい標準ショット")]
        [TestCase("CharacterHint", 1, "CHESHIRE：広範囲・軽いショット")]
        [TestCase("CharacterHint", int.MaxValue, "DORMOUSE：高威力・集中ショット")]
        public void CatalogNamesClampToSupportedContent(string method, int value, string expected)
        {
            Assert.AreEqual(expected, Catalog(method, value));
        }

        [TestCase(int.MinValue, int.MinValue, "「白兎の急降下」")]
        [TestCase(2, 1, "「砂糖菓子の嵐」")]
        [TestCase(int.MaxValue, int.MaxValue, "「裁きのハート」")]
        public void SpellNamesClampStageAndPhase(int stage, int phase, string expected)
        {
            Assert.AreEqual(expected, Catalog("SpellCardName", stage, phase));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ShotProfilesKeepTheirPowerBoundariesAndSharedInstances(int character)
        {
            object low = Catalog("GetShotProfile", character, 1);
            object second = Catalog("GetShotProfile", character, 2);
            object third = Catalog("GetShotProfile", character, 3);
            object high = Catalog("GetShotProfile", character, 4);
            Assert.AreSame(low, Catalog("GetShotProfile", character, int.MinValue));
            Assert.AreSame(high, Catalog("GetShotProfile", character, int.MaxValue));
            Assert.AreNotSame(second, third);
            if (character == 0) { Assert.AreNotSame(low, second); Assert.AreNotSame(third, high); }
            else { Assert.AreSame(low, second); Assert.AreSame(third, high); }
        }

        [TestCase(1)]
        [TestCase(4)]
        public void ShotProfilesClampTheCharacterIndex(int power)
        {
            Assert.AreSame(Catalog("GetShotProfile", 0, power), Catalog("GetShotProfile", int.MinValue, power));
            Assert.AreSame(Catalog("GetShotProfile", 2, power), Catalog("GetShotProfile", int.MaxValue, power));
        }

        private static object Catalog(string method, params object[] arguments)
        {
            return typeof(ProductRules).Assembly.GetType("AliceMirrorfall.GameCatalog").GetMethod(method).Invoke(null, arguments);
        }
    }
}
