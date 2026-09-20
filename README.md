# Tent House Business Management Application
*(टेंट हाउस एवं शामियाना व्यापार प्रबंधन प्रणाली)*

A lightweight, simple, and professional full-stack web application designed for Tent House & Event Rental businesses. Built with high-performance ASP.NET Core (.NET 10) and an embedded SQLite database (`tenthouse.db`).

---

## 🌟 Key Features

### 1. 🌐 Public Customer Portal (`http://localhost:5000`)
- **Festive & Professional Design**: Royal theme with English & Hindi friendly labels (*शामियाना, मंडप, कुर्सियां, सजावट पैकेज*).
- **Services Showcase**: Detailed overview of Waterproof Shamiyana, Wedding Mandaps, VIP Furniture, Sound & DJ Setup, Catering, and Silent Generators.
- **Rental Items Catalog**:
  - Filter by 8 categories (*Tents & Shamiyana, Chairs & Seating, Tables & Dining, Stage & Mandap, Lighting & Sound, Cooling & Power, Catering & Utensils, Carpets & Drapes*).
  - Search items by Hindi or English names.
  - Transparent item display: photo, available stock quantity, and daily rental rate (₹).
  - Quantity steppers with instant "Add to Estimate" basket.
- **All-in-One Decoration Packages**:
  - Pre-assembled packages (Grand Royal Wedding, Haldi & Mehendi Utsav, Reception & Sangeet, Birthday Party, Religious Katha Pandal) with bulleted inclusions and pricing.
- **Interactive Live Estimate Cart**:
  - Sticky bottom calculator bar showing real-time selected items, count, and total rental estimate in ₹.
  - Drawer modal to view, change quantities, or remove items.
- **Booking / Enquiry Submission**:
  - Form fields: Customer Name, 10-Digit Mobile Number, Event Date (with min-date validation), Event Type, Venue / Location, and Special Requirements.
  - Real-time submission saving into SQLite database with unique reference ID (e.g. `TH-2026-1004`).
- **Direct WhatsApp Integration**:
  - One-click button that generates a pre-formatted WhatsApp enquiry message including customer details, date, venue, selected items, package, and total estimate.
- **Floating Contact Buttons**: Quick Call & WhatsApp hotline buttons for mobile users.

---

### 2. 🔐 Private Admin Management Portal (`http://localhost:5000/admin/login.html`)
- **Secure Authentication**:
  - Session cookie-based authentication with PBKDF2 SHA-256 hashed passwords.
  - Route guards: Non-authenticated public users cannot open `/admin/index.html` or access `/api/admin/*` endpoints.
  - Default login credentials:
    - **Username**: `admin`
    - **Password**: `admin123`
- **Dashboard & Income Summary**:
  - Key business metrics: Total Bookings, Pending Enquiries, Confirmed Events, Completed Events, Confirmed Revenue (₹), Total Estimated Revenue in Pipeline (₹).
  - Quick action table for recent enquiries with 1-click status dropdown.
- **Bookings & Enquiries Management**:
  - Search by Customer Name, Mobile Number, Venue, or Booking Reference ID.
  - Filter by status: `Pending`, `Confirmed`, `Completed`, `Cancelled`.
  - Full Details Modal: View itemized breakdown, package selection, and customer instructions.
  - Status & Advance Payment updater: Track advance received (₹) and internal admin notes.
  - **Printable Booking Slip / Invoice**: One-click professional printable quotation/invoice slip complete with tent house letterhead, customer details, itemized rental table, advance paid, balance due, terms, and signature blocks.
- **Rental Inventory Management**:
  - Add New Item modal with image URL, stock quantity, rental price (₹/day), unit, and Hindi name.
  - Edit existing items or toggle active/inactive status.
  - Delete items with confirmation.
- **Decoration Packages Management**:
  - Add, edit, or remove decoration packages and inclusions.
- **Business Profile & Settings**:
  - Update Tent House Name, Tagline, Calling Phone, WhatsApp number, and Address.
  - Change Admin Password with validation.

---

## 🚀 How to Run

### Quick Start (Windows)
Double-click the `run.bat` file in the project folder. It will:
1. Start the web application server on port 5000.
2. Automatically launch your default browser to `http://localhost:5000`.

### Via Terminal
```powershell
# Restore and run
dotnet run
```
Then open:
- Public Website: `http://localhost:5000`
- Admin Portal: `http://localhost:5000/admin/login.html`

---

## 📂 Project Structure

```
New Project/
├── Program.cs             # ASP.NET Core minimal API routes, auth & middleware
├── TentHouseApp.csproj    # .NET 10 project file
├── run.bat                # 1-click launcher script
├── Models/
│   └── Models.cs          # Data models (TentItem, Package, Booking, Settings, etc.)
├── Services/
│   └── Database.cs        # SQLite database service, migrations, and seed data
└── wwwroot/
    ├── index.html         # Public customer portal
    ├── css/
    │   └── styles.css     # Unified modern responsive stylesheet (Royal gold & navy)
    ├── js/
    │   └── app.js         # Public customer UI logic, cart, validation & WhatsApp
    └── admin/
        ├── login.html     # Secure admin login
        ├── index.html     # Admin dashboard & management portal
        └── admin.js       # Admin CRUD logic, status updates & printable receipts
```
