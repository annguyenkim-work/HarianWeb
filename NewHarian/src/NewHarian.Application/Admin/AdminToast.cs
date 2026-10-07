namespace NewHarian.Application.Admin;

public enum AdminToastKind
{
    Success,
    Error
}

public sealed record AdminToastMessage(AdminToastKind Kind, string Text);

/// <summary>
/// Success/failure feedback shown after Admin actions. Error text is the business message from the service;
/// blank errors fall back to a generic message so the user always sees why nothing happened.
/// </summary>
public static class AdminToast
{
    public const string DefaultError = "Không thực hiện được. Vui lòng thử lại.";
    public const string InvalidForm = "Không lưu được. Vui lòng kiểm tra các trường báo lỗi.";

    public static string ErrorOrDefault(string? error)
        => string.IsNullOrWhiteSpace(error) ? DefaultError : error.Trim();

    /// <summary>
    /// Toasts for a rendered Admin page. A POST that re-renders the form with validation errors gets an error toast
    /// (first form-level message, else <see cref="InvalidForm"/>) unless an explicit error is already queued.
    /// </summary>
    public static IReadOnlyList<AdminToastMessage> Resolve(
        string? success,
        string? error,
        bool isPostWithInvalidForm,
        IEnumerable<string>? formLevelErrors = null)
    {
        var list = new List<AdminToastMessage>(2);
        if (!string.IsNullOrWhiteSpace(success))
            list.Add(new AdminToastMessage(AdminToastKind.Success, success.Trim()));

        if (!string.IsNullOrWhiteSpace(error))
        {
            list.Add(new AdminToastMessage(AdminToastKind.Error, error.Trim()));
        }
        else if (isPostWithInvalidForm)
        {
            var first = formLevelErrors?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            list.Add(new AdminToastMessage(AdminToastKind.Error, first?.Trim() ?? InvalidForm));
        }

        return list;
    }
}
