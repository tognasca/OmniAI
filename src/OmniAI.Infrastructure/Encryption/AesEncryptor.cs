using System.Security.Cryptography;
using System.Text;

namespace OmniAI.Infrastructure.Encryption;

/// <summary>
/// AES-256-CBC para criptografar tokens sensíveis no banco de dados.
/// Em desenvolvimento: InitializeDev() usa chave fixa derivada (não criptografa de fato).
/// Em produção: Initialize() com chaves do Azure Key Vault.
/// </summary>
public static class AesEncryptor
{
    private static byte[]? _key;
    private static byte[]? _iv;
    private static bool _devMode;

    /// <summary>Inicializa com chaves base64 reais (produção/Docker).</summary>
    public static void Initialize(string base64Key, string base64Iv)
    {
        try
        {
            _key = Convert.FromBase64String(base64Key);
            _iv = Convert.FromBase64String(base64Iv);

            if (_key.Length != 32)
                throw new ArgumentException($"AES key must be 32 bytes (got {_key.Length})");
            if (_iv.Length != 16)
                throw new ArgumentException($"AES IV must be 16 bytes (got {_iv.Length})");

            _devMode = false;
        }
        catch (FormatException)
        {
            // Chave inválida — cai para modo dev automaticamente com aviso
            Console.WriteLine("[WRN] Encryption key is not valid Base64 — falling back to dev mode (no encryption).");
            InitializeDev();
        }
    }

    /// <summary>Modo desenvolvimento: sem criptografia real, facilita debug.</summary>
    public static void InitializeDev()
    {
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(Encoding.UTF8.GetBytes("OmniAI-Dev-Key-Not-For-Production-Use"));
        _iv = MD5.HashData(Encoding.UTF8.GetBytes("OmniAI-Dev-IV==="));
        _devMode = true;
        Console.WriteLine("[WRN] AesEncryptor running in DEV MODE — tokens are NOT encrypted.");
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        EnsureInitialized();
        if (_devMode) return plainText; // não criptografa em dev

        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.IV = _iv!;
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
        if (_devMode) return cipherText; // valor já está em texto plano

        try
        {
            using var aes = Aes.Create();
            aes.Key = _key!;
            aes.IV = _iv!;
            var buffer = Convert.FromBase64String(cipherText);
            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
        catch
        {
            return cipherText; // valor não estava criptografado (dados antigos)
        }
    }

    private static void EnsureInitialized()
    {
        if (_key is null || _iv is null)
            InitializeDev(); // fallback seguro se ninguém chamou Initialize()
    }
}