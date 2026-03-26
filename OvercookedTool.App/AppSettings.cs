namespace OvercookedTool.App;

internal sealed class AppSettings
{
    public List<string> RecentPackagePaths { get; set; } = new();
    public bool EnableAutoDetectOnImport { get; set; } = true;
    public int MaxRecentCount { get; set; } = 20;
    public bool EnableLogging { get; set; } = true;
    public int MaxBackupPerSave { get; set; } = 10;
    public string AboutVersion { get; set; } = "Dev";
    public string AboutQqGroup { get; set; } = "156986240";
    public string AboutGithubUrl { get; set; } = "https://github.com/StaryDreamer/OvercookedSaveTool";
    public string AboutBilibiliUrl { get; set; } = "https://www.bilibili.com/video/BV1Fq4y1Y7gu";
    public string AboutAuthor { get; set; } = "星夜逐梦";
}
