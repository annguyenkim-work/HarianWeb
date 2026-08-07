namespace NewHarian.Application.Email;

public static class EmailTemplateCodes
{
    public const string OrderCustomer = "order.customer";
    public const string OrderStaff = "order.staff";
    public const string OrderCancelStaff = "order.cancel.staff";
    public const string InquiryCustomer = "inquiry.customer";
    public const string InquiryStaff = "inquiry.staff";
    public const string ApplicationCustomer = "application.customer";
    public const string ApplicationStaff = "application.staff";
    public const string BookingCustomer = "booking.customer";
    public const string BookingStaff = "booking.staff";
}

public record EmailTemplateListItemDto(int Id, string Code, string Name, DateTime? UpdatedAt);

public record EmailTemplateEditDto(
    int Id,
    string Code,
    string Name,
    string PlaceholdersHelp,
    string SubjectTemplate,
    string BodyHtml);

public class EmailTemplateSaveRequest
{
    public int Id { get; set; }
    public string SubjectTemplate { get; set; } = "";
    public string BodyHtml { get; set; } = "";
}

public interface IEmailTemplateService
{
    Task<IReadOnlyList<EmailTemplateListItemDto>> ListAsync(CancellationToken ct = default);
    Task<EmailTemplateEditDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SaveAsync(EmailTemplateSaveRequest request, CancellationToken ct = default);

    /// <summary>
    /// Render subject + body by replacing {{Key}} from vars.
    /// Falls back to built-in default if DB row missing.
    /// </summary>
    Task<(string Subject, string Body)> RenderAsync(
        string code,
        IReadOnlyDictionary<string, string?> vars,
        CancellationToken ct = default);
}
