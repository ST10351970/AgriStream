using AgriStream.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public class AgriStreamDbContext : DbContext
{
    public AgriStreamDbContext(DbContextOptions<AgriStreamDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<IrrigationCommand> IrrigationCommands => Set<IrrigationCommand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.RoleId);
            e.HasIndex(r => r.RoleName).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.UserId);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasOne(u => u.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(u => u.RoleId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.HasKey(d => d.DeviceId);
        });

        modelBuilder.Entity<IrrigationCommand>(e =>
        {
            e.ToTable("IrrigationCommands");
            e.HasKey(c => c.CommandId);
            e.Property(c => c.CommandId).ValueGeneratedNever();
            e.HasOne(c => c.Device)
             .WithMany()
             .HasForeignKey(c => c.DeviceId);
        });
    }
}