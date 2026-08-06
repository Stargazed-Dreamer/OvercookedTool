using System.Text.Json;
using System.Text.Json.Serialization;

namespace OvercookedTool.App;

/// <summary>
/// 存储关于应用程序的版本信息、作者信息和相关链接。
/// </summary>
internal sealed class AboutContent
{
    /// <summary>
    /// 应用程序的版本号。
    /// </summary>
    public string Version { get; init; } = "Dev";

    /// <summary>
    /// 相关的QQ群号码或信息。
    /// </summary>
    public string QqGroup { get; init; } = "null";

    /// <summary>
    /// 项目源代码的GitHub仓库链接。
    /// </summary>
    public string GithubUrl { get; init; } = "null";

    /// <summary>
    /// 相关内容的Bilibili视频链接。
    /// </summary>
    public string BilibiliUrl { get; init; } = "null";

    /// <summary>
    /// 应用程序的作者。
    /// </summary>
    public string Author { get; init; } = "星夜逐梦";
}

/// <summary>
/// 提供关于内容数据的加载功能，从嵌入的JSON资源中读取并返回AboutContent对象。
/// </summary>
internal static class AboutContentProvider
{
    public static AboutContent Load()
    {
        try
        {
            // 获取包含当前类的程序集
            var asm = typeof(AboutContentProvider).Assembly;
            // 查找以"about_content.json"结尾的嵌入资源名称
            var resName = asm.GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith("about_content.json", StringComparison.OrdinalIgnoreCase));
            // 如果没有找到资源，返回默认对象
            if (string.IsNullOrWhiteSpace(resName))
            {
                return new AboutContent();
            }

            // 获取资源的流
            using var stream = asm.GetManifestResourceStream(resName);
            // 如果流不可用，返回默认对象
            if (stream is null)
            {
                return new AboutContent();
            }

            // 从流中读取JSON字符串
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            // 反序列化为原始数据对象，如果为null则使用新实例
            var raw = JsonSerializer.Deserialize<AboutContentRaw>(json) ?? new AboutContentRaw();
            // 构建并返回AboutContent对象，使用默认值填充空字段
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
            // 捕获所有异常，返回默认对象，确保方法不会抛出异常
            return new AboutContent();
        }
    }

    // 用于反序列化JSON的原始数据类
    private sealed class AboutContentRaw
    {
        // 版本号
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        // QQ群号
        [JsonPropertyName("qq_group")]
        public string QqGroup { get; init; } = string.Empty;

        // GitHub仓库URL
        [JsonPropertyName("github_url")]
        public string GithubUrl { get; init; } = string.Empty;

        // Bilibili视频URL
        [JsonPropertyName("bilibili_url")]
        public string BilibiliUrl { get; init; } = string.Empty;

        // 作者名称
        [JsonPropertyName("author")]
        public string Author { get; init; } = string.Empty;
    }
}
