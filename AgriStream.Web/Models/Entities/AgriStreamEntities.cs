namespace AgriStream.Web.Models.Entities;

// Maps to dbo.Roles (Farmer, Agronomist, Admin)
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

//Maps to dbo.Users. Named AppUser to avoid clashing with Controller.User
public class AppUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

//Maps to dbo.Devices - the master device registry
public class Device
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = "Idle";
    public Guid? LastCommandId { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

// Maps to dbo.IrrigationCommands - written by the Queue-triggered Function
public class IrrigationCommand
{
    public Guid CommandId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Device? Device { get; set; }
    public int DurationMinutes { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}