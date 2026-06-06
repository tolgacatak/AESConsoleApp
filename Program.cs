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
        AesPacket? sonPaket = null;

        while (true)
        {
            var secim = MenuGoster(sonPaket is not null);

            switch (secim)
            {
                case 1:
                    sonPaket = SifreleAkisi();
                    break;
                case 2:
                    SifreCozAkisi(sonPaket);
                    break;
                case 3:
                    NasilCalisirAkisi();
                    break;
                case 0:
                    Yaz("\nGörüşürüz! 👋\n", ConsoleColor.Cyan);
                    return;
                default:
                    Yaz("\n  Geçersiz seçim, tekrar dene.\n", ConsoleColor.Red);
                    break;
            }

            Yaz("\n  Devam etmek için bir tuşa bas...", ConsoleColor.DarkGray);
            if (Console.IsInputRedirected) Console.ReadLine();
            else Console.ReadKey(true);
        }
    }

    private static int MenuGoster(bool cozulebilir)
    {
        Console.Clear();
        Yaz("""

               ╔══════════════════════════════════════════════╗
               ║            🔐  AES-256 ŞİFRELEME  🔐           ║
               ╚══════════════════════════════════════════════╝

            """, ConsoleColor.Cyan);

        Yaz("    [1]  Metin şifrele", ConsoleColor.Green);
        Yaz(cozulebilir
            ? "    [2]  Şifre çöz  (son şifrelenen hazır ✔)"
            : "    [2]  Şifre çöz  (Base64 + parola gir)", ConsoleColor.Yellow);
        Yaz("    [3]  AES nasıl çalışır?  (perde arkası)", ConsoleColor.Magenta);
        Yaz("    [0]  Çıkış", ConsoleColor.DarkGray);

        Console.Write("\n    Seçimin: ");
        return int.TryParse(Console.ReadLine(), out int s) ? s : -1;
    }

    // ─────────────────────────── ŞİFRELE ────────────────────────────

    private static AesPacket? SifreleAkisi()
    {
        Baslik("METİN ŞİFRELE");

        string metin = Sor("  Şifrelenecek metin");
        if (string.IsNullOrEmpty(metin))
        {
            Yaz("  Boş metin şifrelenemez.", ConsoleColor.Red);
            return null;
        }

        string parola = Sor("  Parola");
        if (string.IsNullOrEmpty(parola))
        {
            Yaz("  Parola boş olamaz.", ConsoleColor.Red);
            return null;
        }

        AesPacket paket = AesHelper.Encrypt(metin, parola);

        Yaz("\n  ✔ Şifreleme tamamlandı!\n", ConsoleColor.Green);
        Yaz("  Base64 (salt + IV + şifreli metin):", ConsoleColor.DarkGray);
        Yaz("  " + paket.ToBase64(), ConsoleColor.White);

        Yaz("\n  💡 İpucu: Menüden [2] ile bu metni hemen çözebilirsin.", ConsoleColor.DarkGray);
        return paket;
    }

    // ────────────────────────── ŞİFRE ÇÖZ ───────────────────────────

    private static void SifreCozAkisi(AesPacket? sonPaket)
    {
        Baslik("ŞİFRE ÇÖZ");

        AesPacket paket;
        if (sonPaket is not null && Onayla("  Son şifrelenen metni mi çözelim?"))
        {
            paket = sonPaket;
        }
        else
        {
            string b64 = Sor("  Base64 şifreli metin");
            if (!AesPacket.TryFromBase64(b64, out AesPacket? cozulen) || cozulen is null)
            {
                Yaz("  Geçersiz Base64 verisi.", ConsoleColor.Red);
                return;
            }

            paket = cozulen;
        }

        // Yanlış parolada menüye dönmek yerine tekrar tekrar parola soralım.
        // Kullanıcı boş bırakırsa vazgeçip menüye döner.
        while (true)
        {
            string parola = Sor("  Parola (boş = vazgeç)");
            if (string.IsNullOrEmpty(parola))
            {
                Yaz("\n  Vazgeçildi, menüye dönülüyor.", ConsoleColor.DarkGray);
                return;
            }

            try
            {
                string acikMetin = AesHelper.Decrypt(paket, parola);
                Yaz("\n  ✔ Şifre çözüldü!\n", ConsoleColor.Green);
                Yaz("  Açık metin:", ConsoleColor.DarkGray);
                Yaz("  " + acikMetin, ConsoleColor.White);
                return;
            }
            catch (CryptographicException)
            {
                // Yanlış parola → kimlik doğrulama / padding hatası.
                Yaz("\n  ✘ Şifre çözülemedi. Yanlış parola ya da bozuk veri.", ConsoleColor.Red);
                Yaz("  Tekrar dene 👇\n", ConsoleColor.Yellow);
            }
        }
    }

    // ─────────────────────── PERDE ARKASI ───────────────────────────

    private static void NasilCalisirAkisi()
    {
        Baslik("AES NASIL ÇALIŞIR?  (PERDE ARKASI)");

        string metin = Sor("  Örnek metin (boş = 'Merhaba AES!')");
        if (string.IsNullOrEmpty(metin)) metin = "Merhaba AES!";
        string parola = Sor("  Parola (boş = 'sunum123')");
        if (string.IsNullOrEmpty(parola)) parola = "sunum123";

        AesPacket paket = AesHelper.Encrypt(metin, parola);

        Yaz("\n  1) Paroladan anahtar türetilir (PBKDF2 / 100.000 tur):", ConsoleColor.Yellow);
        Yaz($"     Salt (16B)  : {ToHex(paket.Salt)}", ConsoleColor.White);
        Yaz($"     Key  (256b) : {ToHex(paket.DerivedKey)}", ConsoleColor.White);

        Yaz("\n  2) Rastgele IV (başlangıç vektörü) üretilir:", ConsoleColor.Yellow);
        Yaz($"     IV   (16B)  : {ToHex(paket.IV)}", ConsoleColor.White);

        Yaz("\n  3) Açık metin → şifreli metin (CBC modu):", ConsoleColor.Yellow);
        Yaz($"     Girdi  : \"{metin}\"", ConsoleColor.Gray);
        Yaz($"     UTF-8  : {ToHex(Encoding.UTF8.GetBytes(metin))}", ConsoleColor.Gray);
        Yaz("     Şifreli:", ConsoleColor.Gray);
        HexDump(paket.CipherText);

        Yaz("\n  4) Hepsi paketlenir (salt|IV|şifreli) ve Base64 olur:", ConsoleColor.Yellow);
        Yaz("     " + paket.ToBase64(), ConsoleColor.White);

        Yaz("\n  Not: Aynı metni aynı parolayla tekrar şifrele → salt ve IV", ConsoleColor.DarkGray);
        Yaz("       rastgele olduğu için sonuç her seferinde FARKLI çıkar. 🎲", ConsoleColor.DarkGray);
    }

    // ──────────────────────── YARDIMCILAR ───────────────────────────

    private static void Baslik(string baslik)
    {
        Console.Clear();
        Yaz($"\n  ── {baslik} ──\n", ConsoleColor.Cyan);
    }

    private static string Sor(string etiket)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{etiket}: ");
        Console.ResetColor();
        return Console.ReadLine() ?? string.Empty;
    }

    private static bool Onayla(string soru)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"{soru} (e/h): ");
        Console.ResetColor();
        string? c = Console.ReadLine()?.Trim().ToLowerInvariant();
        return c is "" or "e" or "evet" or "y" or "yes";
    }

    private static void Yaz(string metin, ConsoleColor renk)
    {
        Console.ForegroundColor = renk;
        Console.WriteLine(metin);
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
        byte[] birlesik = new byte[Salt.Length + IV.Length + CipherText.Length];
        Buffer.BlockCopy(Salt, 0, birlesik, 0, Salt.Length);
        Buffer.BlockCopy(IV, 0, birlesik, Salt.Length, IV.Length);
        Buffer.BlockCopy(CipherText, 0, birlesik, Salt.Length + IV.Length, CipherText.Length);
        return Convert.ToBase64String(birlesik);
    }

    /// <summary>Base64 dizesini tekrar salt / IV / şifreli metin parçalarına ayırır.</summary>
    public static bool TryFromBase64(string b64, out AesPacket? paket)
    {
        paket = null;
        try
        {
            byte[] birlesik = Convert.FromBase64String(b64.Trim());
            if (birlesik.Length < AesHelper.SaltSize + AesHelper.IvSize) return false;

            byte[] salt = birlesik[..AesHelper.SaltSize];
            byte[] iv = birlesik[AesHelper.SaltSize..(AesHelper.SaltSize + AesHelper.IvSize)];
            byte[] cipher = birlesik[(AesHelper.SaltSize + AesHelper.IvSize)..];

            paket = new AesPacket
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
