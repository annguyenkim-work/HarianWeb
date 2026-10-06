using Microsoft.AspNetCore.Hosting;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Media;
using NSubstitute;

namespace NewHarian.UnitTests.Infrastructure.Media;

[Trait("Category", "Unit")]
public class MediaPathResolverTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "nh-media-unit");
    private static readonly string WebRoot = Path.Combine(Root, "wwwroot");
    private static readonly string AppData = Path.Combine(Root, "App_Data");

    private static IWebHostEnvironment Env()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(Root);
        env.WebRootPath.Returns(WebRoot);
        return env;
    }

    [Fact]
    public void Private_prefix_resolves_under_App_Data_not_wwwroot()
    {
        var abs = MediaPathResolver.GetAbsolutePath(Env(), "private/applications/abc.pdf", isPrivate: true);

        Assert.Equal(Path.Combine(AppData, "private", "applications", "abc.pdf"), abs);
        Assert.DoesNotContain(Path.Combine("wwwroot", "uploads"), abs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Private_prefix_wins_even_if_flag_is_false_and_accepts_backslashes()
    {
        var abs = MediaPathResolver.GetAbsolutePath(Env(), "private\\applications\\abc.pdf", isPrivate: false);

        Assert.Equal(Path.Combine(AppData, "private", "applications", "abc.pdf"), abs);
    }

    [Theory]
    [InlineData("/uploads/products/a.png")]
    [InlineData("uploads/products/a.png")]
    public void Public_upload_urls_resolve_under_wwwroot(string storedPath)
    {
        var abs = MediaPathResolver.GetAbsolutePath(Env(), storedPath, isPrivate: true);

        Assert.Equal(Path.Combine(WebRoot, "uploads", "products", "a.png"), abs);
    }

    [Fact]
    public void Unprefixed_path_follows_private_flag()
    {
        Assert.Equal(Path.Combine(AppData, "legacy", "cv.pdf"),
            MediaPathResolver.GetAbsolutePath(Env(), "legacy/cv.pdf", isPrivate: true));
        Assert.Equal(Path.Combine(WebRoot, "legacy", "logo.png"),
            MediaPathResolver.GetAbsolutePath(Env(), "/legacy/logo.png", isPrivate: false));
    }

    [Fact]
    public void Media_overload_uses_entity_fields()
    {
        var media = new MediaFile { StoredPath = "private/applications/x.pdf", IsPrivate = true };

        Assert.Equal(Path.Combine(AppData, "private", "applications", "x.pdf"), MediaPathResolver.GetAbsolutePath(Env(), media));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_stored_path_throws(string storedPath)
    {
        Assert.Throws<InvalidOperationException>(() => MediaPathResolver.GetAbsolutePath(Env(), storedPath, isPrivate: true));
    }

    [Fact]
    public void Traversal_in_stored_path_escapes_root_and_is_caught_by_IsUnderRoot()
    {
        var abs = MediaPathResolver.GetAbsolutePath(Env(), "private/../../secret.txt", isPrivate: true);

        Assert.False(MediaPathResolver.IsUnderRoot(abs, AppData));
        Assert.False(MediaPathResolver.IsUnderRoot(abs, WebRoot));
    }

    [Fact]
    public void IsUnderRoot_accepts_files_and_nested_folders_inside_root()
    {
        Assert.True(MediaPathResolver.IsUnderRoot(Path.Combine(AppData, "a.pdf"), AppData));
        Assert.True(MediaPathResolver.IsUnderRoot(Path.Combine(AppData, "private", "x", "a.pdf"), AppData));
        Assert.True(MediaPathResolver.IsUnderRoot(Path.Combine(AppData, "a.pdf"), AppData + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void IsUnderRoot_rejects_parent_traversal_sibling_prefix_and_root_itself()
    {
        Assert.False(MediaPathResolver.IsUnderRoot(Path.Combine(AppData, "..", "wwwroot", "a.png"), AppData));
        Assert.False(MediaPathResolver.IsUnderRoot(Path.Combine(Root, "App_Data-evil", "a.pdf"), AppData));
        Assert.False(MediaPathResolver.IsUnderRoot(AppData, AppData));
    }

    [Fact]
    public void Url_and_stored_path_builders_trim_slashes()
    {
        Assert.Equal("/uploads/products/a.png", MediaPathResolver.ToPublicUrl("/products/", "a.png"));
        Assert.Equal("private/applications/a.pdf", MediaPathResolver.ToPrivateStoredPath("applications/", "a.pdf"));
    }
}
