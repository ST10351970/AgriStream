using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models.Entities;
using AgriStream.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AgriStreamDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AgriStreamDbContext db, IPasswordHasher<AppUser> hasher, ILogger<AccountController> logger)
    {
        _db = db;
        _hasher = hasher;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.Username);

        var result = user is null || !user.IsActive
            ? PasswordVerificationResult.Failed
            : _hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Failed sign-in attempt for {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        // this upgrades old hashes automatically if hashing algorithm change
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user!.PasswordHash = _hasher.HashPassword(user, model.Password);
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user!.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName ?? user.Username),
            new(ClaimTypes.Role, user.Role.RoleName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        _logger.LogInformation("User {Username} signed in with role {Role}", user.Username, user.Role.RoleName);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}