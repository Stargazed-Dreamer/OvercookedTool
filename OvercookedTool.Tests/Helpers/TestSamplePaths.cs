using System.IO;
using System.Reflection;

namespace OvercookedTool.Tests.Helpers;

/// <summary>
/// 测试样本路径定位助手。在测试输出目录与解决方案目录之间查找样本文件夹，
/// 让测试既能在 IDE 中跑，也能在 CI 中跑。
/// </summary>
internal static class TestSamplePaths
{
    /// <summary>
    /// 仓库根目录（基于测试程序集位置向上查找，定位到包含 .sln 的目录）。
    /// </summary>
    public static string RepoRoot { get; } = ResolveRepoRoot();

    /// <summary>
    /// 测试 Fixtures 目录（位于测试项目下的 Fixtures/）。
    /// </summary>
    public static string FixturesDir { get; } = Path.Combine(RepoRoot, "OvercookedTool.Tests", "Fixtures");

    /// <summary>
    /// 仓库内自带的 OC2 真实存档目录（虚假 SteamID64 76561198000000002，混合了多个账户的存档）。
    /// 若不存在则测试应跳过。
    /// </summary>
    public static string BuiltInOc2SampleDir { get; } = Path.Combine(RepoRoot, "76561198000000002");

    /// <summary>
    /// 仓库内 OC2 样本的 SteamID64 密钥（目录名即密钥）。
    /// </summary>
    public const string BuiltInOc2SampleKey = "76561198000000002";

    /// <summary>
    /// 参考/我的存档/OC2 目录（用户提供，完整同账户含所有 DLC，参考目录被 .gitignore 排除）。
    /// </summary>
    public static string UserOc2SampleDir { get; } = Path.Combine(RepoRoot, "参考", "我的存档", "OC2", "76561198000000001");

    /// <summary>
    /// 参考/我的存档/AYCE 目录（用户提供，AYCE Steam 版加密二进制存档）。
    /// </summary>
    public static string UserAyceSampleDir { get; } = Path.Combine(RepoRoot, "参考", "我的存档", "AYCE", "76561198000000001");

    /// <summary>
    /// 用户样本的 SteamID64 密钥（目录名即密钥）。
    /// </summary>
    public const string UserSampleKey = "76561198000000001";

    /// <summary>
    /// 他人提供的 OC2 4星全通+DLC三星存档（zip 解压到 temp 目录，与用户同账户 ID）。
    /// 目录名带 + 后缀以避免与用户自己的存档目录冲突。
    /// </summary>
    public static string OtherOc2SampleDir { get; } = Path.Combine(RepoRoot, "temp", "OtherSaves", "Oc2Zip", "76561198000000001+");

    /// <summary>
    /// 他人提供的 AYCE 全通存档（7z 解压到 temp 目录，与用户同账户 ID）。
    /// 包含 BAG/OC1/DLC2-13/DLC101/102/202 等更全面的 DLC。
    /// </summary>
    public static string OtherAyceSampleDir { get; } = Path.Combine(RepoRoot, "temp", "OtherSaves", "Ayce7z", "76561198000000001+");

    /// <summary>
    /// 检查指定路径是否存在。
    /// </summary>
    public static bool IsAvailable(string path) => !string.IsNullOrEmpty(path) && Directory.Exists(path);

    private static string ResolveRepoRoot()
    {
        var assemblyPath = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
        var dir = Path.GetDirectoryName(assemblyPath)!;
        var current = new DirectoryInfo(dir);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OvercookedTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        // 兜底：使用测试项目目录
        return Path.GetFullPath(Path.Combine(dir, "..", ".."));
    }
}
