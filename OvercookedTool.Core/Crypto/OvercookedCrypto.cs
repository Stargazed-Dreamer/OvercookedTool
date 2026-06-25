using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OvercookedTool.Core.Crypto;

/// <summary>
/// 提供基于AES-CBC加密算法的文件加密解密功能，支持CRC32校验和密码派生
/// </summary>
public static class OvercookedCrypto
{
    // 静态盐值字节数组，用于密码派生
    private static readonly byte[] SaltBytes = Encoding.ASCII.GetBytes("jjo+Ffqil5bdpo5VG82kLj8Ng1sK7L/rCqFTa39Zkom2/baqf5j9HMmsuCr0ipjYsPrsaNIOESWy7bDDGYWx1eA==");

    /// <summary>
    /// 解密文件
    /// </summary>
    /// <param name="sourceFile">源文件路径</param>
    /// <param name="destFile">目标文件路径</param>
    /// <param name="password">解密密码</param>
    /// <param name="ignoreCrc">是否忽略CRC校验</param>
    /// <returns>解密是否成功</returns>
    public static bool DecryptFile(string sourceFile, string destFile, string password, bool ignoreCrc)
    {
        var data = File.ReadAllBytes(sourceFile);
        var decrypted = DecryptData(data, password, ignoreCrc);
        if (decrypted is null)
        {
            return false;
        }

        File.WriteAllBytes(destFile, decrypted);
        return true;
    }

    /// <summary>
    /// 加密文件
    /// </summary>
    /// <param name="sourceFile">源文件路径</param>
    /// <param name="destFile">目标文件路径</param>
    /// <param name="password">加密密码</param>
    /// <returns>加密是否成功</returns>
    public static bool EncryptFile(string sourceFile, string destFile, string password)
    {
        var data = File.ReadAllBytes(sourceFile);
        var encrypted = EncryptData(data, password);
        if (encrypted is null)
        {
            return false;
        }

        File.WriteAllBytes(destFile, encrypted);
        return true;
    }

    /// <summary>
    /// 解密数据字节数组
    /// </summary>
    /// <param name="data">要解密的数据</param>
    /// <param name="password">解密密码</param>
    /// <param name="ignoreCrc">是否忽略CRC校验</param>
    /// <returns>解密后的数据，失败返回null</returns>
    public static byte[]? DecryptData(byte[] data, string password, bool ignoreCrc)
    {
        // 数据长度不足20字节时无法包含16字节IV和4字节CRC，直接返回null
        if (data.Length <= 20)
        {
            return null;
        }

        // 验证CRC校验（除非设置忽略校验）
        if (!ignoreCrc && !Crc32.Validate(data, (uint)data.Length - 4))
        {
            return null;
        }

        // 调用反混淆方法解密数据（排除最后4字节CRC）
        return Deobfuscate(data, data.Length - 4, password);
    }

    /// <summary>
    /// 加密数据字节数组
    /// </summary>
    /// <param name="data">要加密的数据</param>
    /// <param name="password">加密密码</param>
    /// <returns>加密后的数据（包含CRC校验），失败返回null</returns>
    public static byte[]? EncryptData(byte[] data, string password)
    {
        if (data.Length == 0)
        {
            return null;
        }

        // 调用混淆方法加密数据
        var encrypted = Obfuscate(data, data.Length, password);
        if (encrypted is null)
        {
            return null;
        }

        // 创建包含加密数据和CRC校验的新数组
        var finalData = new byte[encrypted.Length + Crc32.HashSize];
        Array.Copy(encrypted, finalData, encrypted.Length);
        // 追加CRC校验值
        Crc32.Append(ref finalData, 0, (uint)encrypted.Length);
        return finalData;
    }

    /// <summary>
    /// 尝试将加密数据解密为JSON文本
    /// </summary>
    /// <param name="encryptedData">加密数据</param>
    /// <param name="key">解密密钥</param>
    /// <param name="jsonText">输出的JSON文本</param>
    /// <param name="ignoreCrc">是否忽略CRC校验</param>
    /// <returns>是否成功解密为有效JSON</returns>
    public static bool TryDecryptToJsonText(
        byte[] encryptedData,
        string key,
        out string jsonText,
        bool ignoreCrc = true)
    {
        jsonText = string.Empty;
        var decrypted = DecryptData(encryptedData, key, ignoreCrc);
        if (decrypted is null)
        {
            return false;
        }

        // 将字节数组转换为UTF-8字符串，并移除末尾的空字符
        var text = Encoding.UTF8.GetString(decrypted).TrimEnd('\0');
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            // 验证是否为有效JSON格式
            using var _ = JsonDocument.Parse(text);
            jsonText = text;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 反混淆（解密）方法
    /// </summary>
    /// <param name="obfuscatedText">混淆后的文本</param>
    /// <param name="size">数据大小</param>
    /// <param name="password">密码</param>
    /// <param name="start">起始位置</param>
    /// <param name="keySize">密钥大小（位）</param>
    /// <returns>解密后的字节数组，失败返回null</returns>
    private static byte[]? Deobfuscate(byte[] obfuscatedText, int size, string password, int start = 0, int keySize = 256)
    {
        // 验证输入数据长度是否足够
        if (obfuscatedText.Length <= start + size || obfuscatedText.Length <= 16)
        {
            return null;
        }

        // 提取初始化向量（IV）
        var iv = new byte[16];
        Array.Copy(obfuscatedText, start, iv, 0, 16);

        // 提取密文数据
        var cipher = new byte[size - 16 - start];
        Array.Copy(obfuscatedText, 16, cipher, 0, cipher.Length);

#pragma warning disable SYSLIB0041
        // 使用SHA1进行密码派生，迭代2次
        var key = new PasswordDeriveBytes(password, SaltBytes, "SHA1", 2).GetBytes(keySize / 8);
#pragma warning restore SYSLIB0041

#pragma warning disable SYSLIB0022
        // 创建RijndaelManaged解密器（CBC模式）
        using var rijndael = new RijndaelManaged { Mode = CipherMode.CBC };
#pragma warning restore SYSLIB0022
        var output = new byte[cipher.Length];
        try
        {
            // 创建解密转换器
            using var transform = rijndael.CreateDecryptor(key, iv);
            using var memoryStream = new MemoryStream(cipher);
            using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Read);

            // 读取解密后的数据
            for (int read = 0, total = 0; (read = cryptoStream.Read(output, total, output.Length - total)) != 0; total += read)
            {
            }

            return output;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 混淆（加密）方法
    /// </summary>
    /// <param name="plainText">明文数据</param>
    /// <param name="size">数据大小</param>
    /// <param name="password">密码</param>
    /// <param name="start">起始位置</param>
    /// <param name="keySize">密钥大小（位）</param>
    /// <returns>加密后的字节数组（包含IV），失败返回null</returns>
    private static byte[]? Obfuscate(byte[] plainText, int size, string password, int start = 0, int keySize = 256)
    {
        // 验证输入数据
        if (plainText.Length == 0 || start + size > plainText.Length)
        {
            return null;
        }

        // 生成随机初始化向量（IV）
        var iv = new byte[16];
        Random.Shared.NextBytes(iv);

#pragma warning disable SYSLIB0041
        // 使用SHA1进行密码派生，迭代2次
        var key = new PasswordDeriveBytes(password, SaltBytes, "SHA1", 2).GetBytes(keySize / 8);
#pragma warning restore SYSLIB0041

#pragma warning disable SYSLIB0022
        // 创建RijndaelManaged加密器（CBC模式）
        using var rijndael = new RijndaelManaged { Mode = CipherMode.CBC };
#pragma warning restore SYSLIB0022
        byte[]? cipher;
        try
        {
            // 创建加密转换器
            using var transform = rijndael.CreateEncryptor(key, iv);
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);

            // 写入加密数据并刷新最终块
            cryptoStream.Write(plainText, start, size);
            cryptoStream.FlushFinalBlock();
            cipher = memoryStream.ToArray();
        }
        catch
        {
            return null;
        }

        // 组合IV和密文
        var output = new byte[16 + cipher.Length];
        Array.Copy(iv, output, 16);
        Array.Copy(cipher, 0, output, 16, cipher.Length);
        return output;
    }

    /// <summary>
    /// CRC32校验和计算器
    /// </summary>
    private sealed class Crc32
    {
        // CRC哈希大小（4字节）
        public const uint HashSize = 4u;
        // CRC多项式
        private const uint Poly = 1491524015u;
        // CRC初始种子值
        private const uint Seed = 3605721660u;
        // 预计算的CRC查找表
        private static readonly uint[] Table = MakeTable();

        /// <summary>
        /// 验证数据的CRC校验
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="size">要验证的数据大小</param>
        /// <returns>CRC校验是否通过</returns>
        public static bool Validate(byte[] buffer, uint size)
        {
            var hashStart = size;
            if (hashStart + HashSize > buffer.Length)
            {
                return false;
            }

            // 计算数据的CRC值
            var expected = Calculate(buffer, 0, size);
            // 从缓冲区读取存储的CRC值
            var actual = BitConverter.ToUInt32(buffer, (int)hashStart);
            return expected == actual;
        }

        /// <summary>
        /// 为缓冲区追加CRC校验值
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="start">数据起始位置</param>
        /// <param name="size">数据大小</param>
        public static void Append(ref byte[] buffer, uint start, uint size)
        {
            var hash = Calculate(buffer, start, size);
            var bytes = BitConverter.GetBytes(hash);
            // 如果缓冲区空间不足，则扩展缓冲区
            if (buffer.Length < start + size + HashSize)
            {
                Array.Resize(ref buffer, (int)(start + size + HashSize));
            }

            var offset = (int)size;
            for (var i = 0; i < bytes.Length; i++)
            {
                buffer[offset + i] = bytes[i];
            }
        }

        /// <summary>
        /// 计算数据的CRC32值
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="start">起始位置</param>
        /// <param name="size">数据大小</param>
        /// <returns>CRC32哈希值</returns>
        private static uint Calculate(byte[] data, uint start, uint size)
        {
            var hash = Seed;
            // 使用查找表进行CRC计算
            for (uint i = start; i < start + size; i++)
            {
                hash = (hash >> 8) ^ Table[data[i] ^ (hash & 0xFF)];
            }

            return hash;
        }

        /// <summary>
        /// 生成CRC32查找表
        /// </summary>
        /// <returns>CRC32查找表</returns>
        private static uint[] MakeTable()
        {
            var table = new uint[256];
            // 预计算所有可能的字节值的CRC
            for (uint i = 0; i < 256; i++)
            {
                var value = i;
                for (var b = 0; b < 8; b++)
                {
                    value = (value & 1) != 1 ? value >> 1 : value ^ Poly;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
