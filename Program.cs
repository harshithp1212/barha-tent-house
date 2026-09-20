using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TentHouseApp.Models;
using TentHouseApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<DatabaseService>();

// Cookie Authentication for secure Admin access
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TentHouseAdminAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect("/admin/login.html");
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Initialize database schema and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    db.Initialize();
}

app.UseRouting();

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Route guard for /admin/ or /admin/index.html
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    if (path == "/admin" || path == "/admin/" || path == "/admin/index.html")
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/admin/login.html");
            return;
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

#region Public Endpoints
var publicGroup = app.MapGroup("/api/public");

// Get active rental items
publicGroup.MapGet("/items", (DatabaseService db) =>
{
    var items = db.GetAllItems(activeOnly: true);
    return Results.Ok(items);
});

// Get active decoration packages
publicGroup.MapGet("/packages", (DatabaseService db) =>
{
    var packages = db.GetAllPackages(activeOnly: true);
    return Results.Ok(packages);
});

// Get public business contact and identity info
publicGroup.MapGet("/settings", (DatabaseService db) =>
{
    var settings = db.GetSettings();
    return Results.Ok(new
    {
        settings.BusinessName,
        settings.Tagline,
        settings.Phone,
        settings.WhatsApp,
        settings.Email,
        settings.Address,
        settings.OperatingHours
    });
});

// Submit booking or estimate enquiry request
publicGroup.MapPost("/bookings", ([FromBody] BookingRequest req, DatabaseService db) =>
{
    // Basic Form Validation
    if (string.IsNullOrWhiteSpace(req.CustomerName))
        return Results.BadRequest(new { error = "Customer name is required / ग्राहक का नाम अनिवार्य है।" });

    if (string.IsNullOrWhiteSpace(req.MobileNumber) || req.MobileNumber.Trim().Length < 10)
        return Results.BadRequest(new { error = "Valid 10-digit mobile number is required / मान्य 10 अंकों का मोबाइल नंबर दर्ज करें।" });

    if (string.IsNullOrWhiteSpace(req.EventDate))
        return Results.BadRequest(new { error = "Event date is required / कार्यक्रम की तारीख चुनें।" });

    if (string.IsNullOrWhiteSpace(req.EventLocation))
        return Results.BadRequest(new { error = "Event location/venue address is required / कार्यक्रम का स्थान/पता दर्ज करें।" });

    // Sanitize phone
    req.MobileNumber = new string(req.MobileNumber.Where(char.IsDigit).ToArray());
    if (req.MobileNumber.Length > 10 && req.MobileNumber.StartsWith("91"))
    {
        req.MobileNumber = req.MobileNumber[2..];
    }

    try
    {
        var booking = db.CreateBooking(req);
        return Results.Ok(new
        {
            success = true,
            bookingRef = booking.BookingRef,
            message = "Booking enquiry received successfully! We will contact you shortly.",
            booking
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});
#endregion

#region Admin Authentication Endpoints
var adminAuthGroup = app.MapGroup("/api/admin");

// Admin Login
adminAuthGroup.MapPost("/login", async ([FromBody] LoginRequest req, HttpContext context, DatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { error = "Username and password are required." });
    }

    var isValid = db.ValidateAdmin(req.Username.Trim(), req.Password);
    if (!isValid)
    {
        return Results.Unauthorized();
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, req.Username.Trim()),
        new(ClaimTypes.Role, "Admin")
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var authProperties = new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
    };

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(claimsIdentity),
        authProperties);

    return Results.Ok(new { success = true, username = req.Username.Trim() });
});

// Admin Logout
adminAuthGroup.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
});

// Check Auth Status
adminAuthGroup.MapGet("/check-auth", (HttpContext context) =>
{
    var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
    return Results.Ok(new
    {
        authenticated = isAuthenticated,
        username = isAuthenticated ? context.User.Identity?.Name : null
    });
});
#endregion

#region Protected Admin Endpoints
var adminApi = app.MapGroup("/api/admin").RequireAuthorization();

// Dashboard Summary
adminApi.MapGet("/dashboard", (DatabaseService db) =>
{
    var summary = db.GetDashboardSummary();
    return Results.Ok(summary);
});

// Items CRUD
adminApi.MapGet("/items", (DatabaseService db) =>
{
    return Results.Ok(db.GetAllItems(activeOnly: false));
});

adminApi.MapPost("/items", ([FromBody] TentItem item, DatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(item.Name))
        return Results.BadRequest(new { error = "Item name is required." });
    if (string.IsNullOrWhiteSpace(item.Category))
        item.Category = "General";

    var id = db.AddItem(item);
    item.Id = id;
    return Results.Ok(item);
});

adminApi.MapPut("/items/{id:int}", (int id, [FromBody] TentItem item, DatabaseService db) =>
{
    item.Id = id;
    var updated = db.UpdateItem(item);
    if (!updated) return Results.NotFound();
    return Results.Ok(item);
});

adminApi.MapDelete("/items/{id:int}", (int id, DatabaseService db) =>
{
    var deleted = db.DeleteItem(id);
    if (!deleted) return Results.NotFound();
    return Results.Ok(new { success = true });
});

// Packages CRUD
adminApi.MapGet("/packages", (DatabaseService db) =>
{
    return Results.Ok(db.GetAllPackages(activeOnly: false));
});

adminApi.MapPost("/packages", ([FromBody] DecorationPackage pkg, DatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(pkg.Title))
        return Results.BadRequest(new { error = "Package title is required." });

    var id = db.AddPackage(pkg);
    pkg.Id = id;
    return Results.Ok(pkg);
});

adminApi.MapPut("/packages/{id:int}", (int id, [FromBody] DecorationPackage pkg, DatabaseService db) =>
{
    pkg.Id = id;
    var updated = db.UpdatePackage(pkg);
    if (!updated) return Results.NotFound();
    return Results.Ok(pkg);
});

adminApi.MapDelete("/packages/{id:int}", (int id, DatabaseService db) =>
{
    var deleted = db.DeletePackage(id);
    if (!deleted) return Results.NotFound();
    return Results.Ok(new { success = true });
});

// Bookings
adminApi.MapGet("/bookings", ([FromQuery] string? status, [FromQuery] string? search, DatabaseService db) =>
{
    var list = db.GetAllBookings(status, search);
    return Results.Ok(list);
});

adminApi.MapGet("/bookings/{id:int}", (int id, DatabaseService db) =>
{
    var booking = db.GetBookingById(id);
    if (booking == null) return Results.NotFound();
    return Results.Ok(booking);
});

adminApi.MapPut("/bookings/{id:int}/status", (int id, [FromBody] UpdateStatusRequest req, DatabaseService db) =>
{
    var validStatuses = new[] { "Pending", "Confirmed", "Completed", "Cancelled" };
    if (!validStatuses.Contains(req.Status))
        return Results.BadRequest(new { error = "Invalid status. Allowed: Pending, Confirmed, Completed, Cancelled" });

    var updated = db.UpdateBookingStatus(id, req.Status, req.AdvancePaid, req.AdminNotes);
    if (!updated) return Results.NotFound();

    return Results.Ok(new { success = true, id, req.Status, req.AdvancePaid });
});

adminApi.MapDelete("/bookings/{id:int}", (int id, DatabaseService db) =>
{
    var deleted = db.DeleteBooking(id);
    if (!deleted) return Results.NotFound();
    return Results.Ok(new { success = true });
});

// Settings & Profile
adminApi.MapGet("/settings", (DatabaseService db) =>
{
    return Results.Ok(db.GetSettings());
});

adminApi.MapPost("/settings", ([FromBody] BusinessSettings settings, DatabaseService db) =>
{
    db.UpdateSettings(settings);
    return Results.Ok(new { success = true, settings });
});

// Change Password
adminApi.MapPost("/change-password", ([FromBody] ChangePasswordRequest req, HttpContext context, DatabaseService db) =>
{
    var username = context.User.Identity?.Name ?? "admin";
    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
    {
        return Results.BadRequest(new { error = "New password must be at least 6 characters long." });
    }

    var changed = db.ChangeAdminPassword(username, req.OldPassword, req.NewPassword);
    if (!changed)
    {
        return Results.BadRequest(new { error = "Current password is incorrect." });
    }

    return Results.Ok(new { success = true, message = "Password changed successfully." });
});
#endregion

app.Run("http://127.0.0.1:5000");
