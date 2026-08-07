using Microsoft.AspNetCore.Mvc;

namespace NewHarian.Web.Areas.Admin;

/// <summary>
/// After list mutations (delete/move/status), return to the same filtered/paged URL when possible.
/// </summary>
public static class AdminListRedirect
{
    public static IActionResult ToRefererOrIndex(Controller controller, object? indexRouteValues = null)
    {
        var referer = controller.Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Authority, controller.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Contains("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Redirect(uri.PathAndQuery);
        }

        return controller.RedirectToAction("Index", indexRouteValues ?? new { area = "Admin" });
    }
}
