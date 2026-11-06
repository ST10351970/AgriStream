using AgriStream.Web.Data;
using AgriStream.Web.Models.Entities;
using AgriStream.Web.Services;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

//Azure SQL (users, roles, device registry)
var sqlConnection = builder.Configuration.GetConnectionString("AgriStreamSql")
    ?? throw new InvalidOperationException("Connection string 'AgriStreamSql' is not configured.");

builder.Services.AddDbContext<AgriStreamDbContext>(options =>
    options.UseSqlServer(sqlConnection, sql =>
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// Azure Storage
var storageConnection = builder.Configuration.GetConnectionString("AgriStreamStorage")
    ?? throw new InvalidOperationException("Connection string 'AgriStreamStorage' is not configured.");

builder.Services.AddSingleton(_ => new BlobServiceClient(storageConnection));
builder.Services.AddSingleton(_ => new TableServiceClient(storageConnection));
builder.Services.AddScoped<DroneImageService>();

// HTTP-triggered Azure Functions client
builder.Services.Configure<FunctionsOptions>(builder.Configuration.GetSection(FunctionsOptions.SectionName));
builder.Services.AddHttpClient<FunctionsApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<FunctionsOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(options.Key))
        client.DefaultRequestHeaders.Add("x-functions-key", options.Key);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Cookie-based authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// seed starter users
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database seeding failed - check the AgriStreamSql connection string and SQL firewall.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();