using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;

namespace NewHarian.Infrastructure.Email;

public static class EmailOutboxRedaction
{
    public const string Placeholder = "<p>[Nội dung bảo mật đã xóa sau khi gửi]</p>";

    /// <summary>Wipes the body of a sensitive message once it will not be sent again.</summary>
    public static void Apply(EmailOutboxMessage message)
    {
        if (message.IsSensitive && message.Status is EmailOutboxStatus.Sent or EmailOutboxStatus.Failed)
            message.HtmlBody = Placeholder;
    }

    /// <summary>Body for the App_Data/outbox copy: secrets stay readable only on a developer machine.</summary>
    public static string ForFileCopy(string htmlBody, bool sensitive, bool isDevelopment)
        => sensitive && !isDevelopment ? Placeholder : htmlBody;
}
