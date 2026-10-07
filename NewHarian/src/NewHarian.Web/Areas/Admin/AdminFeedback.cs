using Microsoft.AspNetCore.Mvc;
using NewHarian.Application.Admin;

namespace NewHarian.Web.Areas.Admin;

/// <summary>
/// Queues a toast for the next rendered Admin page (POST-redirect flows). Fetch/JSON flows return
/// <c>{ ok, message }</c> / <c>{ ok:false, error }</c> instead and the page script shows the toast.
/// </summary>
public static class AdminFeedback
{
    public const string SuccessKey = "Success";
    public const string ErrorKey = "Error";

    public static void FlashSuccess(this Controller controller, string message)
        => controller.TempData[SuccessKey] = message;

    public static void FlashError(this Controller controller, string? error)
        => controller.TempData[ErrorKey] = AdminToast.ErrorOrDefault(error);

    public static void FlashResult(this Controller controller, bool ok, string successMessage, string? error)
    {
        if (ok) controller.FlashSuccess(successMessage);
        else controller.FlashError(error);
    }

    public static JsonResult JsonFail(this Controller controller, string? error)
        => controller.Json(new { ok = false, error = AdminToast.ErrorOrDefault(error) });
}
