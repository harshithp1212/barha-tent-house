namespace TentHouseApp.Models;

public class TentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameHindi { get; set; }
    public string Category { get; set; } = "Tents & Shamiyana";
    public int Quantity { get; set; } = 1;
    public decimal PricePerDay { get; set; } = 0;
    public string Unit { get; set; } = "piece";
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

public class DecorationPackage
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleHindi { get; set; }
    public string Category { get; set; } = "Wedding";
    public decimal Price { get; set; } = 0;
    public string? Description { get; set; }
    public string? Inclusions { get; set; } // Semicolon or newline separated
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

public class Booking
{
    public int Id { get; set; }
    public string BookingRef { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string? EventEndDate { get; set; }
    public string EventType { get; set; } = "Wedding";
    public string EventLocation { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled
    public decimal TotalAmount { get; set; } = 0;
    public decimal AdvancePaid { get; set; } = 0;
    public string? ItemsJson { get; set; }
    public string? AdminNotes { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

public class BookingRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string? EventEndDate { get; set; }
    public string EventType { get; set; } = "Wedding";
    public string EventLocation { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public List<SelectedItemDto> SelectedItems { get; set; } = new();
    public int? SelectedPackageId { get; set; }
    public string? SelectedPackageTitle { get; set; }
    public decimal PackagePrice { get; set; } = 0;
}

public class SelectedItemDto
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PricePerDay { get; set; } = 0;
    public decimal Total => Quantity * PricePerDay;
}

public class DashboardSummary
{
    public int TotalBookings { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public decimal TotalEstimatedRevenue { get; set; }
    public decimal ConfirmedRevenue { get; set; }
    public int TotalInventoryItems { get; set; }
    public int TotalPackages { get; set; }
    public List<Booking> RecentBookings { get; set; } = new();
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class BusinessSettings
{
    public string BusinessName { get; set; } = "Royal Utsav Tent House & Decorators";
    public string Tagline { get; set; } = "Complete Event, Shamiyana & Royal Decoration Solutions";
    public string Phone { get; set; } = "+91 98765 43210";
    public string WhatsApp { get; set; } = "+91 98765 43210";
    public string Email { get; set; } = "royalutsavtent@gmail.com";
    public string Address { get; set; } = "Plot No. 12, Main Mandi Road, Near Royal Palace, Jaipur, Rajasthan";
    public string OperatingHours { get; set; } = "8:00 AM - 10:00 PM (All 7 Days)";
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = "Pending";
    public decimal AdvancePaid { get; set; }
    public string? AdminNotes { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
