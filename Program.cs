using System.Security.Cryptography;
using System.Text;

namespace AESExample;

/// <summary>
///     Sunum için hazırlanmış interaktif AES-256 şifreleme demosu.
///     - Parola tabanlı anahtar üretimi (PBKDF2)
///     - Metin şifreleme / şifre çözme
///     - Şifrelemenin "perde arkası" görselleştirmesi (salt, key, IV, hex dump)
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "AES-256 Şifreleme Demosu";

        // Aynı oturum içinde son şifreleme sonucunu hatırlayalım ki
        // kullanıcı menüden hemen "şifre çöz" diyebilsin.
        AesPacket? lastPacket = null;

        while (true)
        {
            var choice = ShowMenu(lastPacket is not null);

            switch (choice)
            {
                case 1:
                    lastPacket = EncryptFlow();
                    break;
                case 2:
                    DecryptFlow(lastPacket);
                    break;
                case 3:
                    HowItWorksFlow();
                    break;
                case 0:
                    Write("\nGörüşürüz! 👋\n", ConsoleColor.Cyan);
                    return;
                default:
                    Write("\n  Geçersiz seçim, tekrar dene.\n", ConsoleColor.Red);
                    break;
            }

            Write("\n  Devam etmek için bir tuşa bas...", ConsoleColor.DarkGray);
            if (Console.IsInputRedirected) Console.ReadLine();
            else Console.ReadKey(true);
        }
    }

    private static int ShowMenu(bool decryptable)
    {
        Console.Clear();
        Write("""

               ╔══════════════════════════════════════════════╗
               ║            🔐  AES-256 ŞİFRELEME  🔐           ║
               ╚══════════════════════════════════════════════╝

            """, ConsoleColor.Cyan);

        Write("    [1]  Metin şifrele", ConsoleColor.Green);
        Write(decryptable
            ? "    [2]  Şifre çöz  (son şifrelenen hazır ✔)"
            : "    [2]  Şifre çöz  (Base64 + parola gir)", ConsoleColor.Yellow);
        Write("    [3]  AES nasıl çalışır?  (perde arkası)", ConsoleColor.Magenta);
        Write("    [0]  Çıkış", ConsoleColor.DarkGray);

        Console.Write("\n    Seçimin: ");
        return int.TryParse(Console.ReadLine(), out int n) ? n : -1;
    }

    // ─────────────────────────── ŞİFRELE ────────────────────────────

    private static AesPacket? EncryptFlow()
    {
        PrintTitle("METİN ŞİFRELE");

        var text = Ask("  Şifrelenecek metin");
        if (string.IsNullOrEmpty(text))
        {
            Write("  Boş metin şifrelenemez.", ConsoleColor.Red);
            return null;
        }

        var password = Ask("  Parola");
        if (string.IsNullOrEmpty(password))
        {
            Write("  Parola boş olamaz.", ConsoleColor.Red);
            return null;
        }

        AesPacket packet = AesHelper.Encrypt(text, password);

        Write("\n  ✔ Şifreleme tamamlandı!\n", ConsoleColor.Green);
        Write("  Base64 (salt + IV + şifreli metin):", ConsoleColor.DarkGray);
        Write("  " + packet.ToBase64(), ConsoleColor.White);

        Write("\n  💡 İpucu: Menüden [2] ile bu metni hemen çözebilirsin.", ConsoleColor.DarkGray);
        return packet;
    }

    // ────────────────────────── ŞİFRE ÇÖZ ───────────────────────────

    private static void DecryptFlow(AesPacket? lastPacket)
    {
        PrintTitle("ŞİFRE ÇÖZ");

        AesPacket packet;
        if (lastPacket is not null && Confirm("  Son şifrelenen metni mi çözelim?"))
        {
            packet = lastPacket;
        }
        else
        {
            var b64 = Ask("  Base64 şifreli metin");
            if (!AesPacket.TryFromBase64(b64, out AesPacket? parsed) || parsed is null)
            {
                Write("  Geçersiz Base64 verisi.", ConsoleColor.Red);
                return;
            }

            packet = parsed;
        }

        // Yanlış parolada menüye dönmek yerine tekrar tekrar parola soralım.
        // Kullanıcı boş bırakırsa vazgeçip menüye döner.
        while (true)
        {
            string password = Ask("  Parola (boş = vazgeç)");
            if (string.IsNullOrEmpty(password))
            {
                Write("\n  Vazgeçildi, menüye dönülüyor.", ConsoleColor.DarkGray);
                return;
            }

            try
            {
                string plainText = AesHelper.Decrypt(packet, password);
                Write("\n  ✔ Şifre çözüldü!\n", ConsoleColor.Green);
                Write("  Açık metin:", ConsoleColor.DarkGray);
                Write("  " + plainText, ConsoleColor.White);
                return;
            }
            catch (CryptographicException)
            {
                // Yanlış parola → kimlik doğrulama / padding hatası.
                Write("\n  ✘ Şifre çözülemedi. Yanlış parola ya da bozuk veri.", ConsoleColor.Red);
                Write("  Tekrar dene 👇\n", ConsoleColor.Yellow);
            }
        }
    }

    // ─────────────────────── PERDE ARKASI ───────────────────────────

    private static void HowItWorksFlow()
    {
        PrintTitle("AES NASIL ÇALIŞIR?  (PERDE ARKASI)");

        string text = Ask("  Örnek metin (boş = 'Merhaba AES!')");
        if (string.IsNullOrEmpty(text)) text = "Merhaba AES!";
        string password = Ask("  Parola (boş = 'sunum123')");
        if (string.IsNullOrEmpty(password)) password = "sunum123";

        AesPacket packet = AesHelper.Encrypt(text, password);

        Write("\n  1) Paroladan anahtar türetilir (PBKDF2 / 100.000 tur):", ConsoleColor.Yellow);
        Write($"     Salt (16B)  : {ToHex(packet.Salt)}", ConsoleColor.White);
        Write($"     Key  (256b) : {ToHex(packet.DerivedKey)}", ConsoleColor.White);

        Write("\n  2) Rastgele IV (başlangıç vektörü) üretilir:", ConsoleColor.Yellow);
        Write($"     IV   (16B)  : {ToHex(packet.IV)}", ConsoleColor.White);

        Write("\n  3) Açık metin → şifreli metin (CBC modu):", ConsoleColor.Yellow);
        Write($"     Girdi  : \"{text}\"", ConsoleColor.Gray);
        Write($"     UTF-8  : {ToHex(Encoding.UTF8.GetBytes(text))}", ConsoleColor.Gray);
        Write("     Şifreli:", ConsoleColor.Gray);
        HexDump(packet.CipherText);

        Write("\n  4) Hepsi paketlenir (salt|IV|şifreli) ve Base64 olur:", ConsoleColor.Yellow);
        Write("     " + packet.ToBase64(), ConsoleColor.White);

        Write("\n  Not: Aynı metni aynı parolayla tekrar şifrele → salt ve IV", ConsoleColor.DarkGray);
        Write("       rastgele olduğu için sonuç her seferinde FARKLI çıkar. 🎲", ConsoleColor.DarkGray);
    }

    // ──────────────────────── YARDIMCILAR ───────────────────────────

    private static void PrintTitle(string title)
    {
        Console.Clear();
        Write($"\n  ── {title} ──\n", ConsoleColor.Cyan);
    }

    private static string Ask(string label)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{label}: ");
        Console.ResetColor();
        return Console.ReadLine() ?? string.Empty;
    }

    private static bool Confirm(string question)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{question} (e/h): ");
        Console.ResetColor();
        string? input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return input is "" or "e" or "evet" or "y" or "yes";
    }

    private static void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static string ToHex(byte[] data) =>
        Convert.ToHexString(data).ToLowerInvariant();

    /// <summary>Klasik 16 bayt/satır hex + ASCII dökümü (hacker hissi 😎).</summary>
    private static void HexDump(byte[] data)
    {
        for (int i = 0; i < data.Length; i += 16)
        {
            var hex = new StringBuilder();
            var ascii = new StringBuilder();

            for (int j = 0; j < 16; j++)
            {
                if (i + j < data.Length)
                {
                    byte b = data[i + j];
                    hex.Append($"{b:x2} ");
                    ascii.Append(b is >= 32 and < 127 ? (char)b : '.');
                }
                else
                {
                    hex.Append("   ");
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"     {i:x4}  {hex} |{ascii}|");
            Console.ResetColor();
        }
    }
}

/// <summary>
///     Bir AES şifreleme sonucunun tüm parçalarını taşıyan veri kabı.
///     Şifre çözmek için salt + IV + şifreli metin birlikte gerekir.
/// </summary>
internal sealed class AesPacket
{
    public required byte[] Salt { get; init; }
    public required byte[] IV { get; init; }
    public required byte[] CipherText { get; init; }

    // Görselleştirme amaçlı; üretilen anahtarı da saklıyoruz (gerçek uygulamada saklanmaz!).
    public required byte[] DerivedKey { get; init; }

    /// <summary>salt(16) | IV(16) | şifreli metin → tek bir Base64 dizesi.</summary>
    public string ToBase64()
    {
        byte[] combined = new byte[Salt.Length + IV.Length + CipherText.Length];
        Buffer.BlockCopy(Salt, 0, combined, 0, Salt.Length);
        Buffer.BlockCopy(IV, 0, combined, Salt.Length, IV.Length);
        Buffer.BlockCopy(CipherText, 0, combined, Salt.Length + IV.Length, CipherText.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>Base64 dizesini tekrar salt / IV / şifreli metin parçalarına ayırır.</summary>
    public static bool TryFromBase64(string b64, out AesPacket? packet)
    {
        packet = null;
        try
        {
            byte[] combined = Convert.FromBase64String(b64.Trim());
            if (combined.Length < AesHelper.SaltSize + AesHelper.IvSize) return false;

            byte[] salt = combined[..AesHelper.SaltSize];
            byte[] iv = combined[AesHelper.SaltSize..(AesHelper.SaltSize + AesHelper.IvSize)];
            byte[] cipher = combined[(AesHelper.SaltSize + AesHelper.IvSize)..];

            packet = new AesPacket
            {
                Salt = salt,
                IV = iv,
                CipherText = cipher,
                DerivedKey = Array.Empty<byte>() // çözüm sırasında paroladan yeniden türetilir
            };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>AES-256-CBC + PBKDF2 ile parola tabanlı şifreleme yardımcıları.</summary>
internal static class AesHelper
{
    public const int SaltSize = 16;          // 128 bit salt
    public const int IvSize = 16;            // AES blok boyutu
    private const int KeySize = 32;          // 256 bit anahtar
    private const int Iterations = 100_000;  // PBKDF2 tur sayısı

    public static AesPacket Encrypt(string plainText, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = DeriveKey(password, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // güvenli rastgele IV

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return new AesPacket
        {
            Salt = salt,
            IV = aes.IV,
            CipherText = cipher,
            DerivedKey = key
        };
    }

    public static string Decrypt(AesPacket packet, string password)
    {
        byte[] key = DeriveKey(password, packet.Salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = packet.IV;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(packet.CipherText, 0, packet.CipherText.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>Paroladan PBKDF2 (SHA-256) ile 256-bit AES anahtarı türetir.</summary>
    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
