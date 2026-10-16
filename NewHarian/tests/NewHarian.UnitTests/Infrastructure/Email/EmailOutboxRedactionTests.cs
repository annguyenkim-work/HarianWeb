using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;

namespace NewHarian.UnitTests.Infrastructure.Email;

[Trait("Category", "Unit")]
public class EmailOutboxRedactionTests
{
    private const string Body = "<p>Mã: HRN-7K3M-Q9TZ-4WXA</p>";

    [Theory]
    [InlineData(EmailOutboxStatus.Sent, true)]
    [InlineData(EmailOutboxStatus.Failed, true)]
    [InlineData(EmailOutboxStatus.Pending, false)]
    public void Sensitive_body_is_wiped_once_final(EmailOutboxStatus status, bool wiped)
    {
        var msg = new EmailOutboxMessage { HtmlBody = Body, IsSensitive = true, Status = status };

        EmailOutboxRedaction.Apply(msg);

        Assert.Equal(wiped ? EmailOutboxRedaction.Placeholder : Body, msg.HtmlBody);
    }

    [Theory]
    [InlineData(EmailOutboxStatus.Sent)]
    [InlineData(EmailOutboxStatus.Failed)]
    public void Normal_body_is_kept(EmailOutboxStatus status)
    {
        var msg = new EmailOutboxMessage { HtmlBody = Body, IsSensitive = false, Status = status };

        EmailOutboxRedaction.Apply(msg);

        Assert.Equal(Body, msg.HtmlBody);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void File_copy_hides_secrets_outside_development(bool sensitive, bool isDevelopment, bool redacted)
    {
        var copy = EmailOutboxRedaction.ForFileCopy(Body, sensitive, isDevelopment);

        Assert.Equal(redacted ? EmailOutboxRedaction.Placeholder : Body, copy);
    }
}
