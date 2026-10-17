using NUnit.Framework;

// Pure-data sanity checks on ChallengeLevels.All. No scene or physics needed, unlike
// Bowling/Challenge Check (Assets/Bowling/Editor/BowlingChallengeCheck.cs), which simulates
// each level with Physics.Simulate and is out of scope here.
public class ChallengeLevelTests
{
    [Test]
    public void EveryLevelHasLocalizedTextInBothLanguages()
    {
        foreach (var level in ChallengeLevels.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(level.nameZh));
            Assert.IsFalse(string.IsNullOrEmpty(level.nameEn));
            Assert.IsFalse(string.IsNullOrEmpty(level.descZh));
            Assert.IsFalse(string.IsNullOrEmpty(level.descEn));
        }
    }

    [Test]
    public void EveryLevelAllowsAtLeastOneBall()
    {
        foreach (var level in ChallengeLevels.All)
            Assert.Greater(level.balls, 0);
    }

    [Test]
    public void StarThresholdsAreOrdered()
    {
        foreach (var level in ChallengeLevels.All)
            Assert.LessOrEqual(level.threeStarBalls, level.twoStarBalls);
    }

    [Test]
    public void TargetsAreValidPinNumbersWhenGoalIsKnockTargets()
    {
        foreach (var level in ChallengeLevels.All)
        {
            if (level.goal != ChallengeGoal.KnockTargets) continue;
            Assert.IsNotEmpty(level.targets);
            foreach (int pin in level.targets)
            {
                Assert.GreaterOrEqual(pin, 1);
                Assert.LessOrEqual(pin, 10);
            }
        }
    }

    [Test]
    public void PinRackIsAlwaysTenLong()
    {
        foreach (var level in ChallengeLevels.All)
            Assert.AreEqual(10, level.pins.Length);
    }
}
