using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NewHarian.Application.Abstractions;
using NewHarian.Infrastructure.Identity;

namespace NewHarian.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet("/admin/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home", new { area = "Admin" });

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/admin/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("admin-login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        logger.LogInformation("Login Start Email={Email}", model.Email);
        try
        {
            if (!ModelState.IsValid)
            {
                logger.LogWarning("Login Done rejected Email={Email} Error={Error}", model.Email, "ModelState invalid");
                return View(model);
            }

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user is null || !user.IsActive)
            {
                logger.LogWarning("Login Done rejected Email={Email} Error={Error}", model.Email, "Invalid credentials or inactive");
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                var reason = result.IsLockedOut ? "Locked out" : "Invalid password";
                logger.LogWarning("Login Done rejected Email={Email} Error={Error}", model.Email, reason);
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            if (!await userManager.IsInRoleAsync(user, AppRoles.Admin) &&
                !await userManager.IsInRoleAsync(user, AppRoles.Staff))
            {
                await signInManager.SignOutAsync();
                logger.LogWarning("Login Done rejected Email={Email} Error={Error}", model.Email, "Wrong role");
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(model);
            }

            logger.LogInformation("Login Done Email={Email} UserId={UserId}", model.Email, user.Id);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login Error Email={Email}", model.Email);
            throw;
        }
    }

    [HttpPost("/admin/logout")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
    public async Task<IActionResult> Logout()
    {
        var email = User.Identity?.Name;
        logger.LogInformation("Logout Start Email={Email}", email);
        try
        {
            await signInManager.SignOutAsync();
            logger.LogInformation("Logout Done Email={Email}", email);
            return RedirectToAction(nameof(Login), new { area = "Admin" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout Error Email={Email}", email);
            throw;
        }
    }

    [HttpGet("/admin/access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
