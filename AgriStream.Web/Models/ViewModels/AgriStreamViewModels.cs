using System.ComponentModel.DataAnnotations;
using AgriStream.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AgriStream.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class CreateUserViewModel
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Full name"), StringLength(100)]
    public string? FullName { get; set; }

    [Display(Name = "Role"), Range(1, int.MaxValue, ErrorMessage = "Select a role.")]
    public int RoleId { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Confirm password"), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [ValidateNever] // dropdown data, not posted back
    public IEnumerable<SelectListItem> Roles { get; set; } = Enumerable.Empty<SelectListItem>();
}

public class DevicesIndexViewModel
{
    public IReadOnlyList<Device> Devices { get; set; } = new List<Device>();
    public IReadOnlyList<string> Fields { get; set; } = new List<string>();
    public string? SelectedField { get; set; }
    public IReadOnlyList<IrrigationCommand> RecentCommands { get; set; } = new List<IrrigationCommand>();
}