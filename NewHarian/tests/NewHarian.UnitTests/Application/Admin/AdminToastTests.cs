using NewHarian.Application.Admin;

namespace NewHarian.UnitTests.Application.Admin;

[Trait("Category", "Unit")]
public class AdminToastTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_error_falls_back_to_default_message(string? error)
        => Assert.Equal(AdminToast.DefaultError, AdminToast.ErrorOrDefault(error));

    [Fact]
    public void Business_error_is_kept_and_trimmed()
        => Assert.Equal("Phải còn ít nhất một Super Admin.", AdminToast.ErrorOrDefault("  Phải còn ít nhất một Super Admin.  "));

    [Fact]
    public void Nothing_queued_and_valid_form_gives_no_toast()
        => Assert.Empty(AdminToast.Resolve(null, null, isPostWithInvalidForm: false));

    [Fact]
    public void Success_message_becomes_success_toast()
    {
        var toast = Assert.Single(AdminToast.Resolve("Đã lưu.", null, isPostWithInvalidForm: false));
        Assert.Equal(new AdminToastMessage(AdminToastKind.Success, "Đã lưu."), toast);
    }

    [Fact]
    public void Queued_error_wins_over_form_errors()
    {
        var toast = Assert.Single(AdminToast.Resolve(null, "Lỗi nghiệp vụ", isPostWithInvalidForm: true, ["Lỗi form"]));
        Assert.Equal(new AdminToastMessage(AdminToastKind.Error, "Lỗi nghiệp vụ"), toast);
    }

    [Fact]
    public void Invalid_post_uses_first_non_blank_form_level_error()
    {
        var toast = Assert.Single(AdminToast.Resolve(null, null, isPostWithInvalidForm: true, ["", "Email hoặc mật khẩu không đúng."]));
        Assert.Equal(new AdminToastMessage(AdminToastKind.Error, "Email hoặc mật khẩu không đúng."), toast);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_post_without_form_level_error_uses_generic_hint(bool passEmptyList)
    {
        var toast = Assert.Single(AdminToast.Resolve(null, null, isPostWithInvalidForm: true, passEmptyList ? [] : null));
        Assert.Equal(new AdminToastMessage(AdminToastKind.Error, AdminToast.InvalidForm), toast);
    }

    [Fact]
    public void Invalid_form_on_get_shows_nothing()
        => Assert.Empty(AdminToast.Resolve(null, null, isPostWithInvalidForm: false, ["ignored"]));

    [Fact]
    public void Success_and_error_can_both_show()
    {
        var toasts = AdminToast.Resolve("Đã lưu.", "Gửi email thất bại", isPostWithInvalidForm: false);
        Assert.Equal(new[] { AdminToastKind.Success, AdminToastKind.Error }, toasts.Select(t => t.Kind).ToArray());
    }
}
