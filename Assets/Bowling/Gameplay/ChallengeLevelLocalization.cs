public static class ChallengeLevelLocalization
{
    public static string LocalizedName(this ChallengeLevel level) => GameSettings.Language == "zh" ? level.nameZh : level.nameEn;
    public static string LocalizedDescription(this ChallengeLevel level) => GameSettings.Language == "zh" ? level.descZh : level.descEn;
}
