using System.Security.Cryptography;
using System.Text;
using OvercookedTool.Core.Crypto;
using OvercookedTool.Tests.Helpers;

namespace OvercookedTool.Tests.Crypto;

/// <summary>
/// OvercookedCrypto 加解密单元测试。
/// 覆盖：CRC32 校验、AES-CBC 加解密往返、边界条件、错误密钥、损坏数据。
/// </summary>
public class OvercookedCryptoTests
{
    private const string SamplePassword = "76561198000000002";

    /// <summary>
    /// 加密数据末尾会追加 4 字节 CRC32，验证 CRC 校验通过。
    /// </summary>
    [Fact]
    public void EncryptData_AppendsCrc32_AndValidates()
    {
        var plain = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword);

        Assert.NotNull(encrypted);
        // 加密后至少包含 16 字节 IV + 密文 + 4 字节 CRC
        Assert.True(encrypted!.Length >= 16 + plain.Length + 4);
        // 末尾 4 字节即为 CRC；用 ignoreCrc=false 走完整校验链路
        var decrypted = OvercookedCrypto.DecryptData(encrypted, SamplePassword, ignoreCrc: false);
        Assert.NotNull(decrypted);
        Assert.Equal(plain, decrypted);
    }

    /// <summary>
    /// 加密 -> 解密往返必须还原原文（覆盖多种长度，特别是非块对齐的情况）。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(255)]
    [InlineData(1024)]
    public void EncryptDecrypt_RoundTrip_PreservesPlaintext(int plainLength)
    {
        var plain = new byte[plainLength];
        RandomNumberGenerator.Fill(plain);

        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword);
        Assert.NotNull(encrypted);

        var decrypted = OvercookedCrypto.DecryptData(encrypted!, SamplePassword, ignoreCrc: false);
        Assert.NotNull(decrypted);
        Assert.Equal(plain, decrypted);
    }

    /// <summary>
    /// 同一明文加密两次，由于 IV 随机，密文应不同；但解密后都应得到原文。
    /// </summary>
    [Fact]
    public void Encrypt_SamePlaintext_Twice_YieldsDifferentCiphertextDueToRandomIv()
    {
        var plain = Encoding.UTF8.GetBytes("{\"m_Keys\":[\"Level_1\"],\"m_Entries\":[]}" + new string('x', 50));

        var a = OvercookedCrypto.EncryptData(plain, SamplePassword);
        var b = OvercookedCrypto.EncryptData(plain, SamplePassword);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(a, b);

        Assert.Equal(plain, OvercookedCrypto.DecryptData(a!, SamplePassword, ignoreCrc: false));
        Assert.Equal(plain, OvercookedCrypto.DecryptData(b!, SamplePassword, ignoreCrc: false));
    }

    /// <summary>
    /// 空明文应加密失败（返回 null）。
    /// </summary>
    [Fact]
    public void EncryptData_EmptyInput_ReturnsNull()
    {
        var result = OvercookedCrypto.EncryptData(Array.Empty<byte>(), SamplePassword);
        Assert.Null(result);
    }

    /// <summary>
    /// 数据长度 <= 20（无法容纳 16 字节 IV + 4 字节 CRC）应解密失败。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(15)]
    [InlineData(20)]
    public void DecryptData_TooShortInput_ReturnsNull(int length)
    {
        var data = new byte[length];
        RandomNumberGenerator.Fill(data);
        var result = OvercookedCrypto.DecryptData(data, SamplePassword, ignoreCrc: true);
        Assert.Null(result);
    }

    /// <summary>
    /// CRC 不匹配且 ignoreCrc=false 时应返回 null。
    /// </summary>
    [Fact]
    public void DecryptData_CrcMismatch_StrictMode_ReturnsNull()
    {
        var plain = Encoding.UTF8.GetBytes("payload-with-crc-check");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword)!;

        // 篡改密文中部一字节，使 CRC 失效
        encrypted[20] ^= 0xFF;

        var strict = OvercookedCrypto.DecryptData(encrypted, SamplePassword, ignoreCrc: false);
        Assert.Null(strict);
    }

    /// <summary>
    /// CRC 不匹配但 ignoreCrc=true 时仍尝试解密（应返回非 null 字节，但内容是垃圾）。
    /// </summary>
    [Fact]
    public void DecryptData_CrcMismatch_LenientMode_AttemptsDecryption()
    {
        var plain = Encoding.UTF8.GetBytes("payload-with-crc-check");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword)!;

        // 篡改密文（不改 CRC 字节），CRC 会失败但能解密得到乱码
        encrypted[20] ^= 0xFF;

        var lenient = OvercookedCrypto.DecryptData(encrypted, SamplePassword, ignoreCrc: true);
        Assert.NotNull(lenient);
        // 解密结果不再是原文
        Assert.NotEqual(plain, lenient);
    }

    /// <summary>
    /// 用错误密钥解密：CRC 仍通过（CRC 只覆盖密文），但解密结果是垃圾；
    /// 通过 TryDecryptToJsonText 的 JSON 校验可以发现解密失败。
    /// </summary>
    [Fact]
    public void TryDecryptToJsonText_WrongKey_FailsJsonValidation()
    {
        var plain = Encoding.UTF8.GetBytes("{\"m_Keys\":[\"Level_1\"],\"m_Entries\":[]}");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword)!;

        var ok = OvercookedCrypto.TryDecryptToJsonText(encrypted, "wrong-key-12345", out var json, ignoreCrc: false);

        Assert.False(ok);
        Assert.Equal(string.Empty, json);
    }

    /// <summary>
    /// 用正确密钥解密有效 JSON 应成功并返回原文。
    /// </summary>
    [Fact]
    public void TryDecryptToJsonText_CorrectKey_ReturnsJsonText()
    {
        var plain = Encoding.UTF8.GetBytes("{\"m_Keys\":[\"Level_1\"],\"m_Entries\":[]}");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword)!;

        var ok = OvercookedCrypto.TryDecryptToJsonText(encrypted, SamplePassword, out var json, ignoreCrc: false);

        Assert.True(ok);
        Assert.Equal("{\"m_Keys\":[\"Level_1\"],\"m_Entries\":[]}", json);
    }

    /// <summary>
    /// 解密后是无效 JSON 时 TryDecryptToJsonText 应返回 false（即使解密成功）。
    /// </summary>
    [Fact]
    public void TryDecryptToJsonText_ValidDecryption_InvalidJson_ReturnsFalse()
    {
        var plain = Encoding.UTF8.GetBytes("not a json payload at all");
        var encrypted = OvercookedCrypto.EncryptData(plain, SamplePassword)!;

        var ok = OvercookedCrypto.TryDecryptToJsonText(encrypted, SamplePassword, out var json, ignoreCrc: false);

        Assert.False(ok);
        Assert.Equal(string.Empty, json);
    }

    /// <summary>
    /// 加密文件 -> 解密文件的文件级往返测试。
    /// </summary>
    [Fact]
    public void EncryptFile_DecryptFile_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "oct-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var plainFile = Path.Combine(tempDir, "plain.json");
            var encFile = Path.Combine(tempDir, "plain.save");
            var decFile = Path.Combine(tempDir, "dec.json");
            var plain = "{\"hello\":\"world\",\"n\":[1,2,3]}" + new string(' ', 50);
            File.WriteAllText(plainFile, plain);

            Assert.True(OvercookedCrypto.EncryptFile(plainFile, encFile, SamplePassword));
            Assert.True(File.Exists(encFile));
            Assert.True(OvercookedCrypto.DecryptFile(encFile, decFile, SamplePassword, ignoreCrc: false));
            Assert.Equal(plain, File.ReadAllText(decFile));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
