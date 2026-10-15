using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Dealers;

namespace NewHarian.Infrastructure.Dealers;

/// <summary>
/// HMAC-SHA256 keyed by <c>DealerAccessCode:HashKey</c> (base64, ≥ 32 bytes). Without it a random key is created once
/// under <c>App_Data/keys</c>, which deploys keep. Either way the key never lives in the database, so a DB dump alone
/// cannot be brute-forced offline. Changing the key invalidates every issued code.
/// </summary>
public sealed class HmacDealerAccessCodeHasher : IDealerAccessCodeHasher
{
    public const string ConfigKey = "DealerAccessCode:HashKey";
    private const int MinKeyBytes = 32;

    private readonly Lazy<byte[]> _key;

    public HmacDealerAccessCodeHasher(
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<HmacDealerAccessCodeHasher> logger)
    {
        _key = new Lazy<byte[]>(() => LoadKey(config[ConfigKey], env.ContentRootPath, logger));
    }

    public string Hash(string normalizedCode)
        => Convert.ToHexStringLower(HMACSHA256.HashData(_key.Value, Encoding.UTF8.GetBytes(normalizedCode)));

    private static byte[] LoadKey(string? configured, string contentRoot, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var bytes = Convert.FromBase64String(configured.Trim());
            if (bytes.Length < MinKeyBytes)
                throw new InvalidOperationException($"{ConfigKey} must decode to at least {MinKeyBytes} bytes.");
            return bytes;
        }

        var dir = Path.Combine(contentRoot, "App_Data", "keys");
        var path = Path.Combine(dir, "dealer-access-code.key");
        Directory.CreateDirectory(dir);
        try
        {
            var fresh = RandomNumberGenerator.GetBytes(MinKeyBytes);
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                fs.Write(Encoding.ASCII.GetBytes(Convert.ToBase64String(fresh)));
            logger.LogWarning("{ConfigKey} not configured: generated a key file under App_Data/keys", ConfigKey);
            return fresh;
        }
        catch (IOException) when (File.Exists(path))
        {
            return Convert.FromBase64String(File.ReadAllText(path).Trim());
        }
    }
}
