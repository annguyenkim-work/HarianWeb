using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewHarian.Infrastructure.Dealers;
using NSubstitute;

namespace NewHarian.UnitTests.Infrastructure.Dealers;

[Trait("Category", "Unit")]
public sealed class HmacDealerAccessCodeHasherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nh-hasher-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private HmacDealerAccessCodeHasher Create(string? key)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [HmacDealerAccessCodeHasher.ConfigKey] = key })
            .Build();
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(_root);
        return new HmacDealerAccessCodeHasher(config, env, NullLogger<HmacDealerAccessCodeHasher>.Instance);
    }

    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Same_key_gives_same_64_char_hex_hash()
    {
        var key = Key();
        var a = Create(key).Hash("7K3MQ9TZ4WXA");
        var b = Create(key).Hash("7K3MQ9TZ4WXA");

        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.NotEqual(a, Create(key).Hash("7K3MQ9TZ4WXB"));
    }

    [Fact]
    public void Different_keys_give_different_hashes()
    {
        Assert.NotEqual(Create(Key()).Hash("7K3MQ9TZ4WXA"), Create(Key()).Hash("7K3MQ9TZ4WXA"));
    }

    [Fact]
    public void Hash_is_not_plain_sha256_of_the_code()
    {
        var plain = Convert.ToHexStringLower(SHA256.HashData("7K3MQ9TZ4WXA"u8));
        Assert.NotEqual(plain, Create(Key()).Hash("7K3MQ9TZ4WXA"));
    }

    [Fact]
    public void Short_configured_key_is_rejected()
    {
        var hasher = Create(Convert.ToBase64String(new byte[16]));
        Assert.Throws<InvalidOperationException>(() => hasher.Hash("7K3MQ9TZ4WXA"));
    }

    [Fact]
    public void Without_config_key_file_is_created_once_and_reused()
    {
        var first = Create(null).Hash("7K3MQ9TZ4WXA");
        var keyFile = Path.Combine(_root, "App_Data", "keys", "dealer-access-code.key");
        Assert.True(File.Exists(keyFile));
        var stored = File.ReadAllText(keyFile);

        var second = Create(null).Hash("7K3MQ9TZ4WXA");

        Assert.Equal(first, second);
        Assert.Equal(stored, File.ReadAllText(keyFile));
        Assert.DoesNotContain("7K3MQ9TZ4WXA", stored);
    }
}
