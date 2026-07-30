using OvercookedTool.Core.Models;
using OvercookedTool.Core.Services;

namespace OvercookedTool.Tests.Services;

/// <summary>
/// SaveFileNameHelper 单元测试。
/// 覆盖：modern/legacy/meta 文件名解析、跨平台文件名构建、WithSlot 拷贝。
/// </summary>
public class SaveFileNameHelperTests
{
    // ===== TryParse: Modern format =====

    [Theory]
    [InlineData("CoopSlot_SaveFile_0.save", 0, null, false, "")]
    [InlineData("CoopSlot_SaveFile_3.save", 3, null, false, "")]
    [InlineData("CoopSlot_SaveFile.save", 0, null, false, "")] // 无槽位时默认 0
    [InlineData("DLC2_CoopSlot_SaveFile_0.save", 0, 2, false, "")]
    [InlineData("DLC8_CoopSlot_SaveFile_4.save", 4, 8, false, "")]
    [InlineData("Meta_SaveFile.save", 0, null, true, "")]
    [InlineData("Meta_SaveFile_0.save", 0, null, true, "")]
    [InlineData("Foo_CoopSlot_SaveFile_1.save", 1, null, false, "Foo")]
    [InlineData("Foo_DLC2_CoopSlot_SaveFile_3.save", 3, 2, false, "Foo")]
    [InlineData("CoopSlot_SaveFile_2.json", 2, null, false, "")] // AYCE JSON
    public void TryParse_ModernFormat_ExtractsFields(
        string fileName, int expectedSlot, int? expectedDlc, bool expectedIsMeta, string expectedPrefix)
    {
        var fullPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.True(SaveFileNameHelper.TryParse(fileName, fullPath, out var entry));
        Assert.Equal(expectedSlot, entry.Slot);
        Assert.Equal(expectedDlc, entry.DlcId);
        Assert.Equal(expectedIsMeta, entry.IsMeta);
        Assert.Equal(expectedPrefix, entry.Prefix);
        Assert.Equal(fileName, entry.FileName);
        Assert.Equal(fullPath, entry.FullPath);
    }

    [Theory]
    [InlineData("CAMPAIGNSAVE0", 0, null, false, "")]
    [InlineData("CAMPAIGNSAVE3", 3, null, false, "")]
    [InlineData("DLC2CAMPAIGNSAVE1", 1, 2, false, "")]
    [InlineData("DLC5CAMPAIGNSAVE0", 0, 5, false, "")]
    [InlineData("FooCAMPAIGNSAVE2", 2, null, false, "Foo")]
    [InlineData("CAMPAIGNSAVE", 0, null, false, "")] // 无槽位时默认 0
    public void TryParse_LegacyFormat_ExtractsFields(
        string fileName, int expectedSlot, int? expectedDlc, bool expectedIsMeta, string expectedPrefix)
    {
        var fullPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.True(SaveFileNameHelper.TryParse(fileName, fullPath, out var entry));
        Assert.Equal(expectedSlot, entry.Slot);
        Assert.Equal(expectedDlc, entry.DlcId);
        Assert.Equal(expectedIsMeta, entry.IsMeta);
        Assert.Equal(expectedPrefix, entry.Prefix);
    }

    [Theory]
    [InlineData("meta")]
    [InlineData("META")]
    [InlineData("Meta")]
    public void TryParse_StandaloneMeta_ReturnsMetaEntry(string fileName)
    {
        var fullPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.True(SaveFileNameHelper.TryParse(fileName, fullPath, out var entry));
        Assert.True(entry.IsMeta);
        Assert.Equal(0, entry.Slot);
        Assert.Null(entry.DlcId);
    }

    [Theory]
    [InlineData("random-file.txt")]
    [InlineData("CoopSlot_SaveFile_0.bin")] // 不支持的扩展名
    [InlineData("DLC_X_CoopSlot_SaveFile_0.save")] // DLC 后不是数字
    [InlineData("")]
    [InlineData("Readme.txt")]
    public void TryParse_UnrecognizedNames_ReturnsFalse(string fileName)
    {
        var fullPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.False(SaveFileNameHelper.TryParse(fileName, fullPath, out _));
    }

    // ===== BuildFileName =====

    [Fact]
    public void BuildFileName_Oc2Binary_NonMeta_BuildsModernSaveName()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: false, prefix: "");
        Assert.Equal("CoopSlot_SaveFile_0.save", SaveFileNameHelper.BuildFileName(SavePlatform.Oc2Binary, entry));
    }

    [Fact]
    public void BuildFileName_Oc2Binary_WithDlc_PrefixesDlc()
    {
        var entry = MakeEntry(slot: 4, dlcId: 2, isMeta: false, prefix: "");
        Assert.Equal("DLC2_CoopSlot_SaveFile_4.save", SaveFileNameHelper.BuildFileName(SavePlatform.Oc2Binary, entry));
    }

    [Fact]
    public void BuildFileName_Oc2Binary_WithPrefix_PrefixesGroup()
    {
        var entry = MakeEntry(slot: 1, dlcId: null, isMeta: false, prefix: "Foo");
        Assert.Equal("Foo_CoopSlot_SaveFile_1.save", SaveFileNameHelper.BuildFileName(SavePlatform.Oc2Binary, entry));
    }

    [Fact]
    public void BuildFileName_Oc2Binary_Meta_BuildsMetaSaveFile()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: true, prefix: "");
        Assert.Equal("Meta_SaveFile.save", SaveFileNameHelper.BuildFileName(SavePlatform.Oc2Binary, entry));
    }

    [Fact]
    public void BuildFileName_AyceJson_NonMeta_BuildsJsonName()
    {
        var entry = MakeEntry(slot: 2, dlcId: null, isMeta: false, prefix: "");
        Assert.Equal("CoopSlot_SaveFile_2.json", SaveFileNameHelper.BuildFileName(SavePlatform.AyceJson, entry));
    }

    [Fact]
    public void BuildFileName_AyceJson_Meta_BuildsMetaJsonFile()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: true, prefix: "");
        Assert.Equal("Meta_SaveFile.json", SaveFileNameHelper.BuildFileName(SavePlatform.AyceJson, entry));
    }

    [Fact]
    public void BuildFileName_XboxBinary_NonMeta_BuildsLegacyCampaignSaveName()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: false, prefix: "");
        Assert.Equal("CAMPAIGNSAVE0", SaveFileNameHelper.BuildFileName(SavePlatform.XboxBinary, entry));
    }

    [Fact]
    public void BuildFileName_XboxBinary_WithDlc_PrefixesDlcNoUnderscore()
    {
        var entry = MakeEntry(slot: 1, dlcId: 2, isMeta: false, prefix: "");
        Assert.Equal("DLC2CAMPAIGNSAVE1", SaveFileNameHelper.BuildFileName(SavePlatform.XboxBinary, entry));
    }

    [Fact]
    public void BuildFileName_XboxBinary_Meta_BuildsLowercaseMeta()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: true, prefix: "");
        Assert.Equal("meta", SaveFileNameHelper.BuildFileName(SavePlatform.XboxBinary, entry));
    }

    [Fact]
    public void BuildFileName_SwitchJson_NonMeta_BuildsLegacyCampaignSaveName()
    {
        // Switch 与 Xbox 共享 Legacy 命名
        var entry = MakeEntry(slot: 3, dlcId: null, isMeta: false, prefix: "");
        Assert.Equal("CAMPAIGNSAVE3", SaveFileNameHelper.BuildFileName(SavePlatform.SwitchJson, entry));
    }

    [Fact]
    public void BuildFileName_SwitchJson_Meta_BuildsLowercaseMeta()
    {
        var entry = MakeEntry(slot: 0, dlcId: null, isMeta: true, prefix: "");
        Assert.Equal("meta", SaveFileNameHelper.BuildFileName(SavePlatform.SwitchJson, entry));
    }

    // ===== WithSlot =====

    [Fact]
    public void WithSlot_CopiesAllFields_AndUsesNewSlot()
    {
        var source = new SaveFileEntry
        {
            FileName = "CoopSlot_SaveFile_1.save",
            FullPath = "/tmp/CoopSlot_SaveFile_1.save",
            Size = 1234,
            LastWriteTime = new DateTime(2024, 1, 2, 3, 4, 5),
            Slot = 1,
            DlcId = 5,
            IsMeta = false,
            StarCount = 12,
            Prefix = "Foo",
        };

        var moved = SaveFileNameHelper.WithSlot(source, slot: 9);

        Assert.Equal(9, moved.Slot);
        Assert.Equal(source.FileName, moved.FileName);
        Assert.Equal(source.FullPath, moved.FullPath);
        Assert.Equal(source.Size, moved.Size);
        Assert.Equal(source.LastWriteTime, moved.LastWriteTime);
        Assert.Equal(source.DlcId, moved.DlcId);
        Assert.Equal(source.IsMeta, moved.IsMeta);
        Assert.Equal(source.StarCount, moved.StarCount);
        Assert.Equal(source.Prefix, moved.Prefix);
    }

    // ===== Parse -> Build round-trip =====

    [Theory]
    [InlineData("CoopSlot_SaveFile_0.save", SavePlatform.Oc2Binary)]
    [InlineData("CoopSlot_SaveFile_3.save", SavePlatform.Oc2Binary)]
    [InlineData("DLC2_CoopSlot_SaveFile_0.save", SavePlatform.Oc2Binary)]
    [InlineData("DLC8_CoopSlot_SaveFile_4.save", SavePlatform.Oc2Binary)]
    [InlineData("Meta_SaveFile.save", SavePlatform.Oc2Binary)]
    [InlineData("CoopSlot_SaveFile_2.json", SavePlatform.AyceJson)]
    [InlineData("Meta_SaveFile.json", SavePlatform.AyceJson)]
    public void ParseThenBuild_RoundTrip_PreservesName(string fileName, SavePlatform platform)
    {
        var fullPath = Path.Combine(Path.GetTempPath(), fileName);
        Assert.True(SaveFileNameHelper.TryParse(fileName, fullPath, out var entry));
        var rebuilt = SaveFileNameHelper.BuildFileName(platform, entry);
        Assert.Equal(fileName, rebuilt);
    }

    private static SaveFileEntry MakeEntry(int slot, int? dlcId, bool isMeta, string prefix)
    {
        return new SaveFileEntry
        {
            FileName = "placeholder",
            FullPath = "placeholder",
            Size = 0,
            LastWriteTime = DateTime.MinValue,
            Slot = slot,
            DlcId = dlcId,
            IsMeta = isMeta,
            StarCount = null,
            Prefix = prefix,
        };
    }
}
