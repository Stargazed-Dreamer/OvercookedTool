using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OvercookedTool.Core.Crypto;

public static class OvercookedCrypto
{
    private static readonly byte[] SaltBytes = Encoding.ASCII.GetBytes("jjo+Ffqil5bdpo5VG82kLj8Ng1sK7L/rCqFTa39Zkom2/baqf5j9HMmsuCr0ipjYsPrsaNIOESWy7bDDGYWx1eA==");

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

    public static byte[]? DecryptData(byte[] data, string password, bool ignoreCrc)
    {
        if (data.Length <= 20)
        {
            return null;
        }

        if (!ignoreCrc && !Crc32.Validate(data, (uint)data.Length - 4))
        {
            return null;
        }

        return Deobfuscate(data, data.Length - 4, password);
    }

    public static byte[]? EncryptData(byte[] data, string password)
    {
        if (data.Length == 0)
        {
            return null;
        }

        var encrypted = Obfuscate(data, data.Length, password);
        if (encrypted is null)
        {
            return null;
        }

        var finalData = new byte[encrypted.Length + Crc32.HashSize];
        Array.Copy(encrypted, finalData, encrypted.Length);
        Crc32.Append(ref finalData, 0, (uint)encrypted.Length);
        return finalData;
    }

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

        var text = Encoding.UTF8.GetString(decrypted).TrimEnd('\0');
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
            jsonText = text;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[]? Deobfuscate(byte[] obfuscatedText, int size, string password, int start = 0, int keySize = 256)
    {
        if (obfuscatedText.Length <= start + size || obfuscatedText.Length <= 16)
        {
            return null;
        }

        var iv = new byte[16];
        Array.Copy(obfuscatedText, start, iv, 0, 16);

        var cipher = new byte[size - 16 - start];
        Array.Copy(obfuscatedText, 16, cipher, 0, cipher.Length);

#pragma warning disable SYSLIB0041
        var key = new PasswordDeriveBytes(password, SaltBytes, "SHA1", 2).GetBytes(keySize / 8);
#pragma warning restore SYSLIB0041

#pragma warning disable SYSLIB0022
        using var rijndael = new RijndaelManaged { Mode = CipherMode.CBC };
#pragma warning restore SYSLIB0022
        var output = new byte[cipher.Length];
        try
        {
            using var transform = rijndael.CreateDecryptor(key, iv);
            using var memoryStream = new MemoryStream(cipher);
            using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Read);

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

    private static byte[]? Obfuscate(byte[] plainText, int size, string password, int start = 0, int keySize = 256)
    {
        if (plainText.Length == 0 || start + size > plainText.Length)
        {
            return null;
        }

        var iv = new byte[16];
        Random.Shared.NextBytes(iv);

#pragma warning disable SYSLIB0041
        var key = new PasswordDeriveBytes(password, SaltBytes, "SHA1", 2).GetBytes(keySize / 8);
#pragma warning restore SYSLIB0041

#pragma warning disable SYSLIB0022
        using var rijndael = new RijndaelManaged { Mode = CipherMode.CBC };
#pragma warning restore SYSLIB0022
        byte[]? cipher;
        try
        {
            using var transform = rijndael.CreateEncryptor(key, iv);
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);

            cryptoStream.Write(plainText, start, size);
            cryptoStream.FlushFinalBlock();
            cipher = memoryStream.ToArray();
        }
        catch
        {
            return null;
        }

        var output = new byte[16 + cipher.Length];
        Array.Copy(iv, output, 16);
        Array.Copy(cipher, 0, output, 16, cipher.Length);
        return output;
    }

    private sealed class Crc32
    {
        public const uint HashSize = 4u;
        private const uint Poly = 1491524015u;
        private const uint Seed = 3605721660u;
        private static readonly uint[] Table = MakeTable();

        public static bool Validate(byte[] buffer, uint size)
        {
            var hashStart = size;
            if (hashStart + HashSize > buffer.Length)
            {
                return false;
            }

            var expected = Calculate(buffer, 0, size);
            var actual = BitConverter.ToUInt32(buffer, (int)hashStart);
            return expected == actual;
        }

        public static void Append(ref byte[] buffer, uint start, uint size)
        {
            var hash = Calculate(buffer, start, size);
            var bytes = BitConverter.GetBytes(hash);
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

        private static uint Calculate(byte[] data, uint start, uint size)
        {
            var hash = Seed;
            for (uint i = start; i < start + size; i++)
            {
                hash = (hash >> 8) ^ Table[data[i] ^ (hash & 0xFF)];
            }

            return hash;
        }

        private static uint[] MakeTable()
        {
            var table = new uint[256];
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
