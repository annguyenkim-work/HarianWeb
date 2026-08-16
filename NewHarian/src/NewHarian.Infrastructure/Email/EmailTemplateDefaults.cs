using NewHarian.Application.Email;
using NewHarian.Domain.Entities;

namespace NewHarian.Infrastructure.Email;

/// <summary>Built-in defaults used for seed + render fallback.</summary>
public static class EmailTemplateDefaults
{
    public static IReadOnlyList<EmailTemplate> All() =>
    [
        T(EmailTemplateCodes.OrderCustomer, "Đơn hàng - xác nhận khách",
            "OrderNumber, CustomerName, CustomerEmail, CustomerPhone, CitizenId, ShippingAddress, ProvinceName, PaymentMethod, OrderLinesHtml, SubTotal, ShippingFee, Total, BankBlockHtml",
            "Xác nhận đơn {{OrderNumber}} - Harian",
            """
            <p>Cảm ơn bạn đã đặt hàng.</p>
            <p>Đơn <strong>{{OrderNumber}}</strong></p>
            <p>{{CustomerName}} / {{CustomerEmail}} / {{CustomerPhone}}</p>
            <p>CCCD: {{CitizenId}}</p>
            <p>{{ShippingAddress}}, {{ProvinceName}}</p>
            <p>PT thanh toán: {{PaymentMethod}}</p>
            <p>{{OrderLinesHtml}}</p>
            <p>Tạm tính {{SubTotal}}đ + Ship {{ShippingFee}}đ = <strong>{{Total}}đ</strong></p>
            {{BankBlockHtml}}
            """),

        T(EmailTemplateCodes.OrderStaff, "Đơn hàng - notify staff",
            "OrderNumber, CustomerName, CustomerEmail, CustomerPhone, CitizenId, ShippingAddress, ProvinceName, PaymentMethod, OrderLinesHtml, SubTotal, ShippingFee, Total, AdminUrl",
            "[Harian] Đơn mới {{OrderNumber}}",
            """
            <p>Đơn <strong>{{OrderNumber}}</strong></p>
            <p>{{CustomerName}} / {{CustomerEmail}} / {{CustomerPhone}}</p>
            <p>CCCD: {{CitizenId}}</p>
            <p>{{ShippingAddress}}, {{ProvinceName}}</p>
            <p>PT: {{PaymentMethod}}</p>
            <p>{{OrderLinesHtml}}</p>
            <p>Tạm tính {{SubTotal}}đ + Ship {{ShippingFee}}đ = <strong>{{Total}}đ</strong></p>
            """),

        T(EmailTemplateCodes.OrderCancelStaff, "Đơn hàng - khách hủy (staff)",
            "OrderNumber, CustomerEmail",
            "[Harian] Khách hủy đơn {{OrderNumber}}",
            "<p>Khách đã hủy đơn CK <strong>{{OrderNumber}}</strong> ({{CustomerEmail}}).</p>"),

        T(EmailTemplateCodes.InquiryCustomer, "Liên hệ - cảm ơn khách",
            "CustomerName",
            "Cảm ơn bạn đã liên hệ - Harian",
            """
            <p>Xin chào {{CustomerName}},</p>
            <p>Chúng tôi đã nhận được tin nhắn và sẽ phản hồi sớm.</p>
            """),

        T(EmailTemplateCodes.InquiryStaff, "Liên hệ - notify staff",
            "InquiryId, CustomerName, CustomerEmail, CustomerPhone, Message",
            "[Harian] Liên hệ mới #{{InquiryId}}",
            """
            <p><strong>{{CustomerName}}</strong> ({{CustomerEmail}})</p>
            <p>{{CustomerPhone}}</p>
            <p>{{Message}}</p>
            """),

        T(EmailTemplateCodes.ApplicationCustomer, "Tuyển dụng - cảm ơn ứng viên",
            "CustomerName, JobTitle",
            "Cảm ơn bạn đã ứng tuyển - Harian",
            """
            <p>Xin chào {{CustomerName}},</p>
            <p>Chúng tôi đã nhận hồ sơ ứng tuyển <strong>{{JobTitle}}</strong> và sẽ liên hệ nếu phù hợp.</p>
            """),

        T(EmailTemplateCodes.ApplicationStaff, "Tuyển dụng - notify staff",
            "ApplicationId, CustomerName, CustomerEmail, ApplicationType, JobTitle",
            "[Harian] Hồ sơ tuyển dụng #{{ApplicationId}}",
            """
            <p><strong>{{JobTitle}}</strong></p>
            <p>{{CustomerName}} - {{ApplicationType}}</p>
            <p>{{CustomerEmail}}</p>
            """),

        T(EmailTemplateCodes.BookingCustomer, "Đặt lịch - xác nhận khách",
            "BookingId (mã HAR-SERVICE-XXXX), BookingNumber, ProductName, VariantLabel, CustomerName, CustomerEmail, CustomerPhone, CitizenId, PreferredDate, PreferredTime, ServiceAddress, Notes",
            "Xác nhận đặt lịch {{BookingId}} - Harian",
            """
            <p>Cảm ơn bạn đã đặt lịch.</p>
            <p>Mã đặt lịch <strong>{{BookingId}}</strong>: {{ProductName}} - {{VariantLabel}}</p>
            <p>{{CustomerName}} / {{CustomerEmail}} / {{CustomerPhone}}</p>
            <p>CCCD: {{CitizenId}}</p>
            <p>{{PreferredDate}} {{PreferredTime}}</p>
            <p>{{ServiceAddress}}</p>
            <p>{{Notes}}</p>
            """),

        T(EmailTemplateCodes.BookingStaff, "Đặt lịch - notify staff",
            "BookingId (mã HAR-SERVICE-XXXX), BookingNumber, ProductName, VariantLabel, CustomerName, CustomerEmail, CustomerPhone, CitizenId, PreferredDate, PreferredTime, ServiceAddress, Notes",
            "[Harian] Đặt lịch mới {{BookingId}}",
            """
            <p>Đặt lịch <strong>{{BookingId}}</strong>: {{ProductName}} - {{VariantLabel}}</p>
            <p>{{CustomerName}} / {{CustomerEmail}} / {{CustomerPhone}}</p>
            <p>CCCD: {{CitizenId}}</p>
            <p>{{PreferredDate}} {{PreferredTime}}</p>
            <p>{{ServiceAddress}}</p>
            <p>{{Notes}}</p>
            """),

        T(EmailTemplateCodes.DealerCustomer, "Đại lý - cảm ơn đăng ký",
            "CustomerName",
            "Cảm ơn bạn đã đăng ký đại lý - Harian",
            """
            <p>Xin chào {{CustomerName}},</p>
            <p>Chúng tôi đã nhận hồ sơ đăng ký đại lý và sẽ liên hệ sau khi xét duyệt.</p>
            """),

        T(EmailTemplateCodes.DealerStaff, "Đại lý - notify staff",
            "DealerId, CustomerName, CustomerEmail, CustomerPhone, CitizenId, Address, Message",
            "[Harian] Hồ sơ đại lý mới #{{DealerId}}",
            """
            <p><strong>{{CustomerName}}</strong> ({{CustomerEmail}})</p>
            <p>{{CustomerPhone}}</p>
            <p>CCCD: {{CitizenId}}</p>
            <p>{{Address}}</p>
            <p>{{Message}}</p>
            """)
    ];

    public static (string Subject, string Body) Get(string code)
    {
        var t = All().FirstOrDefault(x => x.Code == code);
        return t is null
            ? ("Harian", "<p></p>")
            : (t.SubjectTemplate, t.BodyHtml);
    }

    private static EmailTemplate T(string code, string name, string help, string subject, string body) => new()
    {
        Code = code,
        Name = name,
        PlaceholdersHelp = help,
        SubjectTemplate = subject.Trim(),
        BodyHtml = body.Trim(),
        CreatedAt = DateTime.UtcNow
    };
}
