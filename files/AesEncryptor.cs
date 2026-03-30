using System.Security.Cryptography;
using System.Text;

namespace OmniAI.Infrastructure.Encryption;

/// <summary>
/// AES-256-CBC encryption for sensitive fields stored in database (AccessToken).
/// Initialize() deve ser chamado no startup com chave do Azure Key Vault.
/// InitializeDev() usa chave fixa — APENAS para desenvolvimento local.
/// </summary>
public static class AesEncryptor
{
    private static byte[]? _key;
    private static byte[]? _iv;
    private static bool _devMode;

    /// <summary>Produção: chave e IV do Key Vault em base64.</summary>
    public static void Initialize(string base64Key, string base64Iv)
    {
        _key     = Convert.FromBase64String(base64Key);
        _iv      = Convert.FromBase64String(base64Iv);
        _devMode = false;

        if (_key.Length != 32)
            throw new ArgumentException("AES key must be 32 bytes (AES-256)");
        if (_iv.Length != 16)
            throw new ArgumentException("AES IV must be 16 bytes");
    }

    /// <summary>
    /// Desenvolvimento local: usa chave fixa derivada do app name.
    /// NÃO usar em produção — tokens ficam vulneráveis.
    /// </summary>
    public static void InitializeDev()
    {
        using var sha = SHA256.Create();
        _key     = sha.ComputeHash(Encoding.UTF8.GetBytes("OmniAI-Dev-Key-Not-For-Production"));
        _iv      = MD5.HashData(Encoding.UTF8.GetBytes("OmniAI-Dev-IV==="));
        _devMode = true;
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        EnsureInitialized();

        // Dev mode: não criptografa para facilitar debug
        if (_devMode) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.IV  = _iv!;

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        var bytes = Encoding.UTF8.GetBytes(plainText);
        cs.Write(bytes, 0, bytes.Length);
        cs.FlushFinalBlock();
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        EnsureInitialized();

        // Dev mode: já está em texto plano
        if (_devMode) return cipherText;

        // Tenta descriptografar; se falhar, retorna o valor original
        // (proteção contra dados antigos não criptografados)
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key!;
            aes.IV  = _iv!;
            var buffer = Convert.FromBase64String(cipherText);
            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch
        {
            return cipherText; // valor não estava criptografado
        }
    }

    private static void EnsureInitialized()
    {
        if (_key is null || _iv is null)
            throw new InvalidOperationException(
                "AesEncryptor not initialized. Call Initialize() or InitializeDev() at startup.");
    }
}
