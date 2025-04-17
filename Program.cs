using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalResearchAssistantV5.Data;
using PersonalResearchAssistantV5.Models;
using System;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Register Encoding Provider for text extraction
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Load OpenAI API Key from appsettings.json
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient(); // Register IHttpClientFactory

// Configure Database Connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Configure Identity with Relaxed Password Policy
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var app = builder.Build();

// Ensure Default Users and Roles Exist
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRolesExist(roleManager);
        await EnsureDefaultUsersExist(userManager, roleManager);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error ensuring default users and roles exist: {ex.Message}");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Ensure Roles Exist
static async Task EnsureRolesExist(RoleManager<IdentityRole> roleManager)
{
    string[] roles = { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

// Ensure Default Users Exist
static async Task EnsureDefaultUsersExist(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    // Create default admin user
    string adminEmail = "admin@example.com";
    string adminPassword = "Admin@1234";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            Name = "Default Admin",
            ContactNumber = "9999999999",
            UserType = "Admin"
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    // Create default normal user
    string userEmail = "user@example.com";
    string userPassword = "User@1234";

    var normalUser = await userManager.FindByEmailAsync(userEmail);
    if (normalUser == null)
    {
        normalUser = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            Name = "Default User",
            ContactNumber = "8888888888",
            UserType = "User"
        };

        var result = await userManager.CreateAsync(normalUser, userPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(normalUser, "User");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(normalUser, "User"))
        {
            await userManager.AddToRoleAsync(normalUser, "User");
        }
    }
}
