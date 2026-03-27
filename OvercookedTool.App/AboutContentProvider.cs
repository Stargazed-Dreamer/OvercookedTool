using System.Text.Json;
using System.Text.Json.Serialization;

namespace OvercookedTool.App;

internal sealed class AboutContent
{
    public string Version { get; init; } = "Dev";
    public string QqGroup { get; init; } = "null";
    public string GithubUrl { get; init; } = "null";
    public string BilibiliUrl { get; init; } = "null";
    public string Author { get; init; } = "星夜逐梦";
}

internal static class AboutContentProvider
{
    public static AboutContent Load()
    {
        try
        {
            var asm = typeof(AboutContentProvider).Assembly;
            var resName = asm.GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith("about_content.json", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(resName))
            {
                return new AboutContent();
            }

            using var stream = asm.GetManifestResourceStream(resName);
            if (stream is null)
            {
                return new AboutContent();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var raw = JsonSerializer.Deserialize<AboutContentRaw>(json) ?? new AboutContentRaw();
            return new AboutContent
            {
                Version = string.IsNullOrWhiteSpace(raw.Version) ? "Dev" : raw.Version,
                QqGroup = string.IsNullOrWhiteSpace(raw.QqGroup) ? "null" : raw.QqGroup,
                GithubUrl = string.IsNullOrWhiteSpace(raw.GithubUrl) ? "null" : raw.GithubUrl,
                BilibiliUrl = string.IsNullOrWhiteSpace(raw.BilibiliUrl) ? "null" : raw.BilibiliUrl,
                Author = string.IsNullOrWhiteSpace(raw.Author) ? "星夜逐梦" : raw.Author,
            };
        }
        catch
        {
            return new AboutContent();
        }
    }

    private sealed class AboutContentRaw
    {
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        [JsonPropertyName("qq_group")]
        public string QqGroup { get; init; } = string.Empty;

        [JsonPropertyName("github_url")]
        public string GithubUrl { get; init; } = string.Empty;

        [JsonPropertyName("bilibili_url")]
        public string BilibiliUrl { get; init; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; init; } = string.Empty;
    }
}
