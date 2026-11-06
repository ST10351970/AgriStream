using AgriStream.Shared;
using AgriStream.Web.Data;
using AgriStream.Web.Models.Entities;
using AgriStream.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

// Admin only user and role management
[Authorize(Roles = AppRoles.Admin)]
public class UsersController : Controller
{
    private readonly AgriStreamDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;

    public UsersController(AgriStreamDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .OrderBy(u => u.Role.RoleName).ThenBy(u => u.Username)
            .ToListAsync();

        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(new CreateUserViewModel { Roles = await GetRoleOptionsAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

        if (!await _db.Roles.AnyAsync(r => r.RoleId == model.RoleId))
            ModelState.AddModelError(nameof(model.RoleId), "Select a valid role.");

        if (!ModelState.IsValid)
        {
            model.Roles = await GetRoleOptionsAsync();
            return View(model);
        }

        var user = new AppUser
        {
            Username = model.Username.Trim(),
            FullName = model.FullName?.Trim(),
            RoleId = model.RoleId
        };
        user.PasswordHash = _hasher.HashPassword(user, model.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"User '{user.Username}' created.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> GetRoleOptionsAsync() =>
        await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new SelectListItem(r.RoleName, r.RoleId.ToString()))
            .ToListAsync();
}