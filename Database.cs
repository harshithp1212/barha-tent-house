using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TentHouseApp.Models;

namespace TentHouseApp.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "tenthouse.db");
        _connectionString = $"Data Source={dbPath}";
    }

    private SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Initialize()
    {
        using var conn = GetConnection();

        // 1. Items table
        var cmdItems = conn.CreateCommand();
        cmdItems.CommandText = @"
            CREATE TABLE IF NOT EXISTS items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                name_hi TEXT,
                category TEXT NOT NULL,
                quantity INTEGER NOT NULL DEFAULT 1,
                price_per_day REAL NOT NULL DEFAULT 0,
                unit TEXT DEFAULT 'piece',
                image_url TEXT,
                description TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_items_category ON items(category);
        ";
        cmdItems.ExecuteNonQuery();

        // 2. Packages table
        var cmdPackages = conn.CreateCommand();
        cmdPackages.CommandText = @"
            CREATE TABLE IF NOT EXISTS packages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                title_hi TEXT,
                category TEXT DEFAULT 'Wedding',
                price REAL NOT NULL DEFAULT 0,
                description TEXT,
                inclusions TEXT,
                image_url TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL
            );
        ";
        cmdPackages.ExecuteNonQuery();

        // 3. Bookings table
        var cmdBookings = conn.CreateCommand();
        cmdBookings.CommandText = @"
            CREATE TABLE IF NOT EXISTS bookings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                booking_ref TEXT UNIQUE NOT NULL,
                customer_name TEXT NOT NULL,
                mobile_number TEXT NOT NULL,
                event_date TEXT NOT NULL,
                event_end_date TEXT,
                event_type TEXT NOT NULL,
                event_location TEXT NOT NULL,
                requirements TEXT,
                status TEXT NOT NULL DEFAULT 'Pending',
                total_amount REAL NOT NULL DEFAULT 0,
                advance_paid REAL NOT NULL DEFAULT 0,
                items_json TEXT,
                admin_notes TEXT,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_bookings_status ON bookings(status);
            CREATE INDEX IF NOT EXISTS idx_bookings_date ON bookings(event_date);
        ";
        cmdBookings.ExecuteNonQuery();

        // 4. Settings table
        var cmdSettings = conn.CreateCommand();
        cmdSettings.CommandText = @"
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        ";
        cmdSettings.ExecuteNonQuery();

        // 5. Admin users table
        var cmdAdmin = conn.CreateCommand();
        cmdAdmin.CommandText = @"
            CREATE TABLE IF NOT EXISTS admins (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                salt TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
        ";
        cmdAdmin.ExecuteNonQuery();

        // Seed default Admin if not exists
        var checkAdmin = conn.CreateCommand();
        checkAdmin.CommandText = "SELECT COUNT(*) FROM admins WHERE username = 'admin';";
        var adminCount = Convert.ToInt32(checkAdmin.ExecuteScalar());
        if (adminCount == 0)
        {
            var salt = GenerateSalt();
            var hash = HashPassword("admin123", salt);
            var insertAdmin = conn.CreateCommand();
            insertAdmin.CommandText = "INSERT INTO admins (username, password_hash, salt, created_at) VALUES ('admin', @hash, @salt, datetime('now'));";
            insertAdmin.Parameters.AddWithValue("@hash", hash);
            insertAdmin.Parameters.AddWithValue("@salt", salt);
            insertAdmin.ExecuteNonQuery();
        }

        // Seed default settings if empty
        var checkSettings = conn.CreateCommand();
        checkSettings.CommandText = "SELECT COUNT(*) FROM settings;";
        if (Convert.ToInt32(checkSettings.ExecuteScalar()) == 0)
        {
            var defaults = new Dictionary<string, string>
            {
                { "BusinessName", "Royal Utsav Tent House & Decorators" },
                { "Tagline", "शाही शामियाना, स्टेज एवं शादी-विवाह सजावट सेवाएँ" },
                { "Phone", "+91 98290 12345" },
                { "WhatsApp", "919829012345" },
                { "Email", "royalutsavtent@gmail.com" },
                { "Address", "Near Subhash Chowk, Main Market Road, Jaipur, Rajasthan 302002" },
                { "OperatingHours", "8:00 AM - 10:00 PM (All 7 Days)" }
            };
            foreach (var kvp in defaults)
            {
                var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO settings (key, value) VALUES (@k, @v);";
                ins.Parameters.AddWithValue("@k", kvp.Key);
                ins.Parameters.AddWithValue("@v", kvp.Value);
                ins.ExecuteNonQuery();
            }
        }

        // Seed initial items if empty
        var checkItems = conn.CreateCommand();
        checkItems.CommandText = "SELECT COUNT(*) FROM items;";
        if (Convert.ToInt32(checkItems.ExecuteScalar()) == 0)
        {
            SeedInitialItems(conn);
        }

        // Seed initial packages if empty
        var checkPkgs = conn.CreateCommand();
        checkPkgs.CommandText = "SELECT COUNT(*) FROM packages;";
        if (Convert.ToInt32(checkPkgs.ExecuteScalar()) == 0)
        {
            SeedInitialPackages(conn);
        }

        // Seed sample bookings for immediate demonstration in admin
        var checkBookings = conn.CreateCommand();
        checkBookings.CommandText = "SELECT COUNT(*) FROM bookings;";
        if (Convert.ToInt32(checkBookings.ExecuteScalar()) == 0)
        {
            SeedSampleBookings(conn);
        }
    }

    private void SeedInitialItems(SqliteConnection conn)
    {
        var items = new List<TentItem>
        {
            // Tents & Shamiyana
            new() { Name = "Waterproof Shamiyana Tent (30x60 ft)", NameHindi = "वाटरप्रूफ शामियाना तंबू", Category = "Tents & Shamiyana", Quantity = 8, PricePerDay = 5500, Unit = "tent", ImageUrl = "https://images.unsplash.com/photo-1519741497674-611481863552?w=600&auto=format&fit=crop&q=80", Description = "Heavy duty German hangar style waterproof shamiyana with side walls and floral frills" },
            new() { Name = "Royal White Pagoda Canopy (20x20 ft)", NameHindi = "शाही सफेद पगोड़ा कैनोपी", Category = "Tents & Shamiyana", Quantity = 15, PricePerDay = 2800, Unit = "tent", ImageUrl = "https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?w=600&auto=format&fit=crop&q=80", Description = "Conical peak pagoda tent ideal for food stalls, VIP lounge, or entrance canopy" },
            new() { Name = "Standard Stall Canopy (10x10 ft)", NameHindi = "छोटा स्टॉल कैनोपी", Category = "Tents & Shamiyana", Quantity = 25, PricePerDay = 900, Unit = "tent", ImageUrl = "https://images.unsplash.com/photo-1533105079780-92b9be482077?w=600&auto=format&fit=crop&q=80", Description = "Portable pop-up canopy for chat stalls, beverage counters, and counters" },
            new() { Name = "Traditional Pandal Shamiyana (40x80 ft)", NameHindi = "पारंपरिक पंडाल शामियाना", Category = "Tents & Shamiyana", Quantity = 5, PricePerDay = 9500, Unit = "tent", ImageUrl = "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?w=600&auto=format&fit=crop&q=80", Description = "Grand Rajasthani printed or royal red-gold ceiling cloth pandal with pillared frame" },

            // Chairs & Seating
            new() { Name = "Banquet Chair with White Cover & Gold Ribbon", NameHindi = "कवर व रिबन वाली बैंक्वेट कुर्सी", Category = "Chairs & Seating", Quantity = 600, PricePerDay = 40, Unit = "chair", ImageUrl = "https://images.unsplash.com/photo-1545232979-8bf68ee9b1af?w=600&auto=format&fit=crop&q=80", Description = "Cushioned banquet chairs with iron frame, snow-white fitted cover and satin ribbon bow" },
            new() { Name = "Maharaja Royal High-Back Sofa Set", NameHindi = "महाराजा शाही दूल्हा-दुल्हन सोफा", Category = "Chairs & Seating", Quantity = 6, PricePerDay = 2500, Unit = "set", ImageUrl = "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?w=600&auto=format&fit=crop&q=80", Description = "Gold-carved royal 2-seater high back sofa designed specifically for Bride and Groom stage" },
            new() { Name = "VIP 2-Seater White Leatherette Sofa", NameHindi = "वीआईपी लाउंज सोफा (2 सीटर)", Category = "Chairs & Seating", Quantity = 24, PricePerDay = 950, Unit = "sofa", ImageUrl = "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?w=600&auto=format&fit=crop&q=80", Description = "Comfortable modern low-profile leatherette sofa for VIP front rows and special guests" },
            new() { Name = "Indian Baithak Diwan with Gaddi & Masand", NameHindi = "गद्दा, चादर व गोल तकिया बैठक सेट", Category = "Chairs & Seating", Quantity = 30, PricePerDay = 450, Unit = "set", ImageUrl = "https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?w=600&auto=format&fit=crop&q=80", Description = "Traditional ground floor seating with 4-inch mattress, white sheet, and 2 round masand bolsters" },
            new() { Name = "Standard Plastic Arm Chairs", NameHindi = "साधारण प्लास्टिक कुर्सियां", Category = "Chairs & Seating", Quantity = 800, PricePerDay = 15, Unit = "chair", ImageUrl = "https://images.unsplash.com/photo-1503602642458-232111445657?w=600&auto=format&fit=crop&q=80", Description = "Durable premium plastic chairs for dining and general seating" },

            // Tables & Dining
            new() { Name = "Round Banquet Dining Table (5 ft)", NameHindi = "गोल डाइनिंग टेबल (कवर व फ्रिल)", Category = "Tables & Dining", Quantity = 50, PricePerDay = 280, Unit = "table", ImageUrl = "https://images.unsplash.com/photo-1530103862676-de8c9debad1d?w=600&auto=format&fit=crop&q=80", Description = "5-foot round table seats 6-8 people, includes table linen and pleated skirting" },
            new() { Name = "Rectangular Buffet Catering Table (6x3 ft)", NameHindi = "बुफे कैटरिंग टेबल (6x3 फीट)", Category = "Tables & Dining", Quantity = 60, PricePerDay = 200, Unit = "table", ImageUrl = "https://images.unsplash.com/photo-1527529482837-4698179dc6ce?w=600&auto=format&fit=crop&q=80", Description = "Heavy folding iron buffet table with satin cloth covering and border frill" },
            new() { Name = "VIP Coffee Glass Center Table", NameHindi = "वीआईपी सेंटर टेबल", Category = "Tables & Dining", Quantity = 20, PricePerDay = 180, Unit = "table", ImageUrl = "https://images.unsplash.com/photo-1533090161767-e6ffed986c88?w=600&auto=format&fit=crop&q=80", Description = "Stylish glass top low coffee table to pair with VIP sofa seating" },

            // Stage & Mandap
            new() { Name = "Grand Wooden Wedding Stage Setup (24x16 ft)", NameHindi = "शाही लकड़ी का मंच / स्टेज", Category = "Stage & Mandap", Quantity = 4, PricePerDay = 6500, Unit = "setup", ImageUrl = "https://images.unsplash.com/photo-1519225421980-715cb0215aed?w=600&auto=format&fit=crop&q=80", Description = "Strong iron and wooden framework stage with staircase, skirting and red velvet carpet" },
            new() { Name = "Carved Wedding Mandap with Fabric & Lights", NameHindi = "शाही विवाह मंडप (हवन कुंड सहित)", Category = "Stage & Mandap", Quantity = 3, PricePerDay = 9500, Unit = "setup", ImageUrl = "https://images.unsplash.com/photo-1583939003579-730e3918a45a?w=600&auto=format&fit=crop&q=80", Description = "Traditional 4-pillar carved mandap with fabric drapes, ceiling hanging floral rings and Havan Kund" },
            new() { Name = "Haldi / Mehendi Yellow Floral Jhula Setup", NameHindi = "हल्दी-मेहंदी सजावटी झूला व बैकड्रॉप", Category = "Stage & Mandap", Quantity = 5, PricePerDay = 3800, Unit = "setup", ImageUrl = "https://images.unsplash.com/photo-1544078751-58fee2d8a03b?w=600&auto=format&fit=crop&q=80", Description = "Vibrant marigold & artificial floral swing with yellow printed backdrop and photo props" },
            new() { Name = "Grand Royal Entrance Floral Arch Gate", NameHindi = "शाही मुख्य प्रवेश द्वार (आर्च गेट)", Category = "Stage & Mandap", Quantity = 6, PricePerDay = 3500, Unit = "gate", ImageUrl = "https://images.unsplash.com/photo-1520854221256-17451cc331bf?w=600&auto=format&fit=crop&q=80", Description = "20-foot wide welcome entry tunnel or decorative arch with artificial flower bunches and LED floodlights" },

            // Lighting & Sound
            new() { Name = "Par LED 54-RGB Stage Focus Lights", NameHindi = "पार 54 एलईडी फोकस लाइट", Category = "Lighting & Sound", Quantity = 60, PricePerDay = 250, Unit = "light", ImageUrl = "https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?w=600&auto=format&fit=crop&q=80", Description = "Multi-color changing stage lighting with DMX controller for ambient stage illumination" },
            new() { Name = "Sharpie Moving Head Beam Stage Lights", NameHindi = "शार्पी बीम मूविंग लाइट", Category = "Lighting & Sound", Quantity = 12, PricePerDay = 1200, Unit = "pair", ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=600&auto=format&fit=crop&q=80", Description = "High-power moving beam lights for DJ night, Sangeet stage, and wedding grand entry" },
            new() { Name = "Jhumar Chandelier & Warm Fairy String Lights", NameHindi = "झूमर व गोल्डन लड़ी लाइट सेट", Category = "Lighting & Sound", Quantity = 30, PricePerDay = 450, Unit = "set", ImageUrl = "https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=600&auto=format&fit=crop&q=80", Description = "Ceiling crystal jhumar and 100 meters warm yellow LED fairy drapes for enchanting night ambiance" },
            new() { Name = "Halogen Flood Lights (500W / 1000W)", NameHindi = "हैलोजन फ्लड लाइट (तेज रोशनी)", Category = "Lighting & Sound", Quantity = 40, PricePerDay = 150, Unit = "piece", ImageUrl = "https://images.unsplash.com/photo-1563245372-f21724e3856d?w=600&auto=format&fit=crop&q=80", Description = "Broad area white lighting for parking grounds, dining zones, and food stalls" },
            new() { Name = "Professional Dual Speaker Sound System with Mics", NameHindi = "साउंड सिस्टम (2 स्पीकर व वायरलेस माइक)", Category = "Lighting & Sound", Quantity = 8, PricePerDay = 3200, Unit = "setup", ImageUrl = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80", Description = "JBL/Yamaha 15-inch sound monitors with amplifier, mixer, Bluetooth, and 2 cordless wireless microphones" },

            // Cooling & Power
            new() { Name = "Silent Diesel Generator (25 KVA with Operator)", NameHindi = "साइलेंट डीजल जनरेटर 25 KVA", Category = "Cooling & Power", Quantity = 4, PricePerDay = 4800, Unit = "day", ImageUrl = "https://images.unsplash.com/photo-1581092160607-ee22621dd758?w=600&auto=format&fit=crop&q=80", Description = "Zero noise heavy generator to power entire shamiyana, stage, and cooling without interruption" },
            new() { Name = "Commercial Jumbo Desert Air Cooler", NameHindi = "जंबो इंडस्ट्रियल एयर कूलर", Category = "Cooling & Power", Quantity = 30, PricePerDay = 800, Unit = "cooler", ImageUrl = "https://images.unsplash.com/photo-1585338107529-13afc5f02586?w=600&auto=format&fit=crop&q=80", Description = "Heavy body metal desert cooler with strong airflow and automatic water pump" },
            new() { Name = "Mist Outdoor Water Sprinkler Fan", NameHindi = "मिस्ट वाटर फॉग पंखा", Category = "Cooling & Power", Quantity = 18, PricePerDay = 500, Unit = "fan", ImageUrl = "https://images.unsplash.com/photo-1590725140246-20acbe442779?w=600&auto=format&fit=crop&q=80", Description = "Standing high-velocity mist fan with built-in water tank for pleasant outdoor cooling" },

            // Catering & Utensils
            new() { Name = "Stainless Steel Roll-Top Chafing Dish (Hot Pot)", NameHindi = "स्टेनलेस स्टील शेफिंग डिश (गीजर)", Category = "Catering & Utensils", Quantity = 40, PricePerDay = 180, Unit = "dish", ImageUrl = "https://images.unsplash.com/photo-1555244162-803834f70033?w=600&auto=format&fit=crop&q=80", Description = "Mirror finish stainless food warmer chafing dish with burner holder and food pan" },
            new() { Name = "Melamine Royal Dinner Crockery Set (100 Pcs)", NameHindi = "रॉयल डिनर प्लेट सेट (100 पीस)", Category = "Catering & Utensils", Quantity = 12, PricePerDay = 1100, Unit = "set", ImageUrl = "https://images.unsplash.com/photo-1614707267537-b85aaf00c4b7?w=600&auto=format&fit=crop&q=80", Description = "Includes 100 full plates, 200 bowls (katoris), spoons, and sweet dish plates" },
            new() { Name = "Large Water Dispenser Tank with Stand (100 Ltr)", NameHindi = "बड़ा पानी का टैंक व स्टैंड (100 ली.)", Category = "Catering & Utensils", Quantity = 10, PricePerDay = 250, Unit = "piece", ImageUrl = "https://images.unsplash.com/photo-1548839140-29a749e1bc4e?w=600&auto=format&fit=crop&q=80", Description = "Food grade stainless steel water dispenser with twin tap brass faucets" },

            // Carpets & Drapes
            new() { Name = "Red Velvet Event Carpet Runway (6x50 ft)", NameHindi = "लाल मखमली वीआईपी रेड कार्पेट", Category = "Carpets & Drapes", Quantity = 15, PricePerDay = 650, Unit = "roll", ImageUrl = "https://images.unsplash.com/photo-1549465220-1a8b9238cd48?w=600&auto=format&fit=crop&q=80", Description = "Plush red aisle carpet runner for bridal entry, VIP walkway, and stage border" },
            new() { Name = "Royal Green Artificial Grass Mat (6x50 ft)", NameHindi = "हरी आर्टिफिशियल घास मैट (ग्रीन कार्पेट)", Category = "Carpets & Drapes", Quantity = 12, PricePerDay = 750, Unit = "roll", ImageUrl = "https://images.unsplash.com/photo-1563245372-f21724e3856d?w=600&auto=format&fit=crop&q=80", Description = "Clean high-density green turf carpet for mandap floor, photo zones, and lawn dining" }
        };

        foreach (var it in items)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO items (name, name_hi, category, quantity, price_per_day, unit, image_url, description, is_active, created_at)
                VALUES (@name, @name_hi, @category, @quantity, @price, @unit, @image_url, @desc, 1, datetime('now'));
            ";
            cmd.Parameters.AddWithValue("@name", it.Name);
            cmd.Parameters.AddWithValue("@name_hi", (object?)it.NameHindi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@category", it.Category);
            cmd.Parameters.AddWithValue("@quantity", it.Quantity);
            cmd.Parameters.AddWithValue("@price", it.PricePerDay);
            cmd.Parameters.AddWithValue("@unit", it.Unit);
            cmd.Parameters.AddWithValue("@image_url", (object?)it.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desc", (object?)it.Description ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedInitialPackages(SqliteConnection conn)
    {
        var pkgs = new List<DecorationPackage>
        {
            new()
            {
                Title = "Grand Royal Wedding Shamiyana & Mandap Setup",
                TitleHindi = "शाही विवाह सम्पूर्ण शामियाना व मंडप पैकेज",
                Category = "Wedding",
                Price = 48000,
                Description = "Complete wedding arrangement including heavy waterproof shamiyana, carved royal mandap, stage, VIP seating, and lighting.",
                Inclusions = "40x80 ft Waterproof Shamiyana;Carved Floral Wedding Mandap with Havan Kund;24x16 ft Wooden Stage with Red Velvet Carpet;Maharaja Bride-Groom High Back Sofa Set;200 Banquet Chairs with White Covers & Ribbon;8 Round Dining Tables with frills;12 Buffet Tables with skirting;Welcome Entry Floral Arch Gate;Stage Lighting (8 Par LED + Warm Jhumar);Sound System with 2 Cordless Mics",
                ImageUrl = "https://images.unsplash.com/photo-1519741497674-611481863552?w=600&auto=format&fit=crop&q=80"
            },
            new()
            {
                Title = "Vibrant Haldi & Mehendi Celebration Package",
                TitleHindi = "हल्दी एवं मेहंदी उत्सव पैकेज",
                Category = "Haldi/Mehendi",
                Price = 16500,
                Description = "Colorful yellow & orange themed decor with traditional swings, props, and cozy gaddi baithak seating for family ceremonies.",
                Inclusions = "20x40 ft Bright Yellow Pandal Canopy;Floral Decorated Wooden Jhula (Swing) with Marigolds;Traditional Gaddi Diwan Seating with Masands (8 sets);50 Banquet Chairs with matching covers;Photobooth Floral Backdrop with Props;Fairy String Ambient Lighting;Bluetooth Sound System for Sangeet Songs;2 Jumbo Air Coolers",
                ImageUrl = "https://images.unsplash.com/photo-1544078751-58fee2d8a03b?w=600&auto=format&fit=crop&q=80"
            },
            new()
            {
                Title = "Grand Reception & Musical Sangeet Setup",
                TitleHindi = "भव्य रिसेप्शन एवं संगीत नाइट पैकेज",
                Category = "Reception",
                Price = 32000,
                Description = "Glamorous evening setup featuring high-tech moving beam lights, royal couple sofa, plush seating, and buffet layout.",
                Inclusions = "30x60 ft German Hangar Tent with White Ceiling;20x16 ft Stage with 3D Backdrop Frame;VIP Couple Royal Sofa Set;4 VIP 2-Seater Leatherette Sofas;150 Banquet Chairs with Covers;6 Round Dining Tables + 10 Buffet Counters;Moving Head Sharpie Lights + 8 Par LED;Red Carpet Runway (50 ft);Heavy Sound Setup with Mixer & Mics",
                ImageUrl = "https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?w=600&auto=format&fit=crop&q=80"
            },
            new()
            {
                Title = "Birthday, Anniversary & Family Gathering Package",
                TitleHindi = "जन्मदिन एवं पारिवारिक उत्सव पैकेज",
                Category = "Birthday",
                Price = 8500,
                Description = "Compact, elegant and fast setup for birthday parties, ring ceremonies, and anniversary celebrations.",
                Inclusions = "20x20 ft Pagoda Peak Canopy Tent;Decorative Ring Floral/Balloon Backdrop with Neon Sign;Cake Cutting Table with frill cover;50 Banquet/Plastic Chairs;2 Buffet Catering Tables with linen;Warm Fairy Lights & Par Stage Lights;Sound System for Birthday Music;2 High Velocity Mist Fans",
                ImageUrl = "https://images.unsplash.com/photo-1530103862676-de8c9debad1d?w=600&auto=format&fit=crop&q=80"
            },
            new()
            {
                Title = "Religious Katha, Havan & Puja Pandal Setup",
                TitleHindi = "धार्मिक कथा, हवन एवं पूजा पंडाल",
                Category = "Religious/Puja",
                Price = 14000,
                Description = "Spiritual ambience with clean white gaddi bedding, elevated Vyas Peeth stage, and clear acoustic sound system.",
                Inclusions = "30x60 ft Pandal Shamiyana with clean interior fabric;Vyas Peeth Elevated Stage (12x10 ft);White Gaddi Baithak Bedding for 120 devotees;50 Plastic Chairs for elderly guests;Acoustic Sound System with 4 Horn/Wall Speakers & 2 Microphones;Halogen and Tube Floodlights;2 Jumbo Air Coolers",
                ImageUrl = "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?w=600&auto=format&fit=crop&q=80"
            }
        };

        foreach (var p in pkgs)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO packages (title, title_hi, category, price, description, inclusions, image_url, is_active, created_at)
                VALUES (@title, @title_hi, @category, @price, @desc, @inc, @img, 1, datetime('now'));
            ";
            cmd.Parameters.AddWithValue("@title", p.Title);
            cmd.Parameters.AddWithValue("@title_hi", (object?)p.TitleHindi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@category", p.Category);
            cmd.Parameters.AddWithValue("@price", p.Price);
            cmd.Parameters.AddWithValue("@desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@inc", (object?)p.Inclusions ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@img", (object?)p.ImageUrl ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedSampleBookings(SqliteConnection conn)
    {
        var sampleDate1 = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
        var sampleDate2 = DateTime.Now.AddDays(14).ToString("yyyy-MM-dd");
        var sampleDate3 = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd");

        var sampleItems1 = JsonSerializer.Serialize(new
        {
            Package = "Grand Royal Wedding Shamiyana & Mandap Setup (₹48,000)",
            Items = new[]
            {
                new { Name = "VIP Banquet Chair with White Cover", Quantity = 100, Rate = 40, Total = 4000 },
                new { Name = "Commercial Jumbo Desert Air Cooler", Quantity = 4, Rate = 800, Total = 3200 }
            }
        });

        var sampleItems2 = JsonSerializer.Serialize(new
        {
            Package = "Vibrant Haldi & Mehendi Celebration Package (₹16,500)",
            Items = new[]
            {
                new { Name = "Indian Baithak Diwan with Gaddi", Quantity = 6, Rate = 450, Total = 2700 }
            }
        });

        var sampleItems3 = JsonSerializer.Serialize(new
        {
            Package = "Birthday, Anniversary & Family Gathering Package (₹8,500)",
            Items = new[]
            {
                new { Name = "Mist Outdoor Water Sprinkler Fan", Quantity = 2, Rate = 500, Total = 1000 }
            }
        });

        var bookings = new[]
        {
            new
            {
                Ref = "TH-2026-1001",
                Name = "Rajesh Sharma (राजेश शर्मा)",
                Phone = "9828011223",
                Date = sampleDate1,
                Type = "Wedding",
                Location = "Shubh Mangal Garden, Sikar Road, Jaipur",
                Notes = "Need mandap setup completed by 4 PM. Flower colors should be golden yellow & maroon.",
                Status = "Confirmed",
                Total = 55200m,
                Advance = 15000m,
                Json = sampleItems1
            },
            new
            {
                Ref = "TH-2026-1002",
                Name = "Sunita Verma (सुनीता वर्मा)",
                Phone = "9829144556",
                Date = sampleDate2,
                Type = "Haldi/Mehendi",
                Location = "Community Hall, Malviya Nagar, Jaipur",
                Notes = "Haldi ceremony starts at 11 AM. Require yellow floral backdrop.",
                Status = "Pending",
                Total = 19200m,
                Advance = 0m,
                Json = sampleItems2
            },
            new
            {
                Ref = "TH-2026-1003",
                Name = "Vikram Singh (विक्रम सिंह)",
                Phone = "9414088990",
                Date = sampleDate3,
                Type = "Birthday",
                Location = "Rooftop Terrace, Vaishali Nagar, Jaipur",
                Notes = "1st birthday party celebration setup. Clean arrangement appreciated.",
                Status = "Completed",
                Total = 9500m,
                Advance = 9500m,
                Json = sampleItems3
            }
        };

        foreach (var b in bookings)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO bookings (booking_ref, customer_name, mobile_number, event_date, event_type, event_location, requirements, status, total_amount, advance_paid, items_json, created_at)
                VALUES (@ref, @name, @phone, @date, @type, @loc, @notes, @status, @total, @adv, @json, datetime('now'));
            ";
            cmd.Parameters.AddWithValue("@ref", b.Ref);
            cmd.Parameters.AddWithValue("@name", b.Name);
            cmd.Parameters.AddWithValue("@phone", b.Phone);
            cmd.Parameters.AddWithValue("@date", b.Date);
            cmd.Parameters.AddWithValue("@type", b.Type);
            cmd.Parameters.AddWithValue("@loc", b.Location);
            cmd.Parameters.AddWithValue("@notes", b.Notes);
            cmd.Parameters.AddWithValue("@status", b.Status);
            cmd.Parameters.AddWithValue("@total", b.Total);
            cmd.Parameters.AddWithValue("@adv", b.Advance);
            cmd.Parameters.AddWithValue("@json", b.Json);
            cmd.ExecuteNonQuery();
        }
    }

    #region Items CRUD
    public List<TentItem> GetAllItems(bool activeOnly = false)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = activeOnly
            ? "SELECT * FROM items WHERE is_active = 1 ORDER BY category, name;"
            : "SELECT * FROM items ORDER BY category, name;";

        using var reader = cmd.ExecuteReader();
        var list = new List<TentItem>();
        while (reader.Read())
        {
            list.Add(ReadItem(reader));
        }
        return list;
    }

    public TentItem? GetItemById(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadItem(reader);
        return null;
    }

    public int AddItem(TentItem item)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (name, name_hi, category, quantity, price_per_day, unit, image_url, description, is_active, created_at)
            VALUES (@name, @name_hi, @category, @quantity, @price, @unit, @image_url, @desc, @is_active, datetime('now'));
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@name", item.Name);
        cmd.Parameters.AddWithValue("@name_hi", (object?)item.NameHindi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", item.Category);
        cmd.Parameters.AddWithValue("@quantity", item.Quantity);
        cmd.Parameters.AddWithValue("@price", item.PricePerDay);
        cmd.Parameters.AddWithValue("@unit", item.Unit);
        cmd.Parameters.AddWithValue("@image_url", (object?)item.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", (object?)item.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_active", item.IsActive ? 1 : 0);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateItem(TentItem item)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE items SET
                name = @name,
                name_hi = @name_hi,
                category = @category,
                quantity = @quantity,
                price_per_day = @price,
                unit = @unit,
                image_url = @image_url,
                description = @desc,
                is_active = @is_active
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@name", item.Name);
        cmd.Parameters.AddWithValue("@name_hi", (object?)item.NameHindi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", item.Category);
        cmd.Parameters.AddWithValue("@quantity", item.Quantity);
        cmd.Parameters.AddWithValue("@price", item.PricePerDay);
        cmd.Parameters.AddWithValue("@unit", item.Unit);
        cmd.Parameters.AddWithValue("@image_url", (object?)item.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", (object?)item.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_active", item.IsActive ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteItem(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static TentItem ReadItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("id")),
        Name = reader.GetString(reader.GetOrdinal("name")),
        NameHindi = reader.IsDBNull(reader.GetOrdinal("name_hi")) ? null : reader.GetString(reader.GetOrdinal("name_hi")),
        Category = reader.GetString(reader.GetOrdinal("category")),
        Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
        PricePerDay = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("price_per_day"))),
        Unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? "piece" : reader.GetString(reader.GetOrdinal("unit")),
        ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
        Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
        IsActive = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
        CreatedAt = reader.GetString(reader.GetOrdinal("created_at"))
    };
    #endregion

    #region Packages CRUD
    public List<DecorationPackage> GetAllPackages(bool activeOnly = false)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = activeOnly
            ? "SELECT * FROM packages WHERE is_active = 1 ORDER BY price;"
            : "SELECT * FROM packages ORDER BY price;";

        using var reader = cmd.ExecuteReader();
        var list = new List<DecorationPackage>();
        while (reader.Read())
        {
            list.Add(ReadPackage(reader));
        }
        return list;
    }

    public DecorationPackage? GetPackageById(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM packages WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadPackage(reader);
        return null;
    }

    public int AddPackage(DecorationPackage pkg)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO packages (title, title_hi, category, price, description, inclusions, image_url, is_active, created_at)
            VALUES (@title, @title_hi, @category, @price, @desc, @inc, @img, @is_active, datetime('now'));
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@title", pkg.Title);
        cmd.Parameters.AddWithValue("@title_hi", (object?)pkg.TitleHindi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", pkg.Category);
        cmd.Parameters.AddWithValue("@price", pkg.Price);
        cmd.Parameters.AddWithValue("@desc", (object?)pkg.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@inc", (object?)pkg.Inclusions ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@img", (object?)pkg.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_active", pkg.IsActive ? 1 : 0);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdatePackage(DecorationPackage pkg)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE packages SET
                title = @title,
                title_hi = @title_hi,
                category = @category,
                price = @price,
                description = @desc,
                inclusions = @inc,
                image_url = @img,
                is_active = @is_active
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", pkg.Id);
        cmd.Parameters.AddWithValue("@title", pkg.Title);
        cmd.Parameters.AddWithValue("@title_hi", (object?)pkg.TitleHindi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", pkg.Category);
        cmd.Parameters.AddWithValue("@price", pkg.Price);
        cmd.Parameters.AddWithValue("@desc", (object?)pkg.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@inc", (object?)pkg.Inclusions ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@img", (object?)pkg.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_active", pkg.IsActive ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeletePackage(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM packages WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static DecorationPackage ReadPackage(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("id")),
        Title = reader.GetString(reader.GetOrdinal("title")),
        TitleHindi = reader.IsDBNull(reader.GetOrdinal("title_hi")) ? null : reader.GetString(reader.GetOrdinal("title_hi")),
        Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "Wedding" : reader.GetString(reader.GetOrdinal("category")),
        Price = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("price"))),
        Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
        Inclusions = reader.IsDBNull(reader.GetOrdinal("inclusions")) ? null : reader.GetString(reader.GetOrdinal("inclusions")),
        ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
        IsActive = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
        CreatedAt = reader.GetString(reader.GetOrdinal("created_at"))
    };
    #endregion

    #region Bookings CRUD
    public Booking CreateBooking(BookingRequest req)
    {
        using var conn = GetConnection();
        var bookingRef = $"TH-{DateTime.UtcNow:yyyy}-{Random.Shared.Next(1000, 9999)}";

        decimal total = req.PackagePrice;
        foreach (var item in req.SelectedItems)
        {
            total += item.Total;
        }

        var itemsSummary = new
        {
            Package = req.SelectedPackageTitle != null ? $"{req.SelectedPackageTitle} (₹{req.PackagePrice:N0})" : null,
            Items = req.SelectedItems
        };
        var jsonStr = JsonSerializer.Serialize(itemsSummary);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO bookings (booking_ref, customer_name, mobile_number, event_date, event_end_date, event_type, event_location, requirements, status, total_amount, advance_paid, items_json, created_at)
            VALUES (@ref, @name, @mobile, @date, @end_date, @type, @location, @reqs, 'Pending', @total, 0, @json, datetime('now'));
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@ref", bookingRef);
        cmd.Parameters.AddWithValue("@name", req.CustomerName);
        cmd.Parameters.AddWithValue("@mobile", req.MobileNumber);
        cmd.Parameters.AddWithValue("@date", req.EventDate);
        cmd.Parameters.AddWithValue("@end_date", (object?)req.EventEndDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@type", req.EventType);
        cmd.Parameters.AddWithValue("@location", req.EventLocation);
        cmd.Parameters.AddWithValue("@reqs", (object?)req.Requirements ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@total", total);
        cmd.Parameters.AddWithValue("@json", jsonStr);

        var id = Convert.ToInt32(cmd.ExecuteScalar());

        return new Booking
        {
            Id = id,
            BookingRef = bookingRef,
            CustomerName = req.CustomerName,
            MobileNumber = req.MobileNumber,
            EventDate = req.EventDate,
            EventEndDate = req.EventEndDate,
            EventType = req.EventType,
            EventLocation = req.EventLocation,
            Requirements = req.Requirements,
            Status = "Pending",
            TotalAmount = total,
            AdvancePaid = 0,
            ItemsJson = jsonStr,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    public List<Booking> GetAllBookings(string? status = null, string? search = null)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        var sb = new StringBuilder("SELECT * FROM bookings WHERE 1=1");

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            sb.Append(" AND status = @status");
            cmd.Parameters.AddWithValue("@status", status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sb.Append(" AND (customer_name LIKE @s OR mobile_number LIKE @s OR event_location LIKE @s OR booking_ref LIKE @s)");
            cmd.Parameters.AddWithValue("@s", $"%{search}%");
        }

        sb.Append(" ORDER BY id DESC;");
        cmd.CommandText = sb.ToString();

        using var reader = cmd.ExecuteReader();
        var list = new List<Booking>();
        while (reader.Read())
        {
            list.Add(ReadBooking(reader));
        }
        return list;
    }

    public Booking? GetBookingById(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM bookings WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadBooking(reader);
        return null;
    }

    public bool UpdateBookingStatus(int id, string status, decimal advancePaid, string? notes)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE bookings SET
                status = @status,
                advance_paid = @advance,
                admin_notes = @notes
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@advance", advancePaid);
        cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteBooking(int id)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM bookings WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static Booking ReadBooking(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("id")),
        BookingRef = reader.GetString(reader.GetOrdinal("booking_ref")),
        CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
        MobileNumber = reader.GetString(reader.GetOrdinal("mobile_number")),
        EventDate = reader.GetString(reader.GetOrdinal("event_date")),
        EventEndDate = reader.IsDBNull(reader.GetOrdinal("event_end_date")) ? null : reader.GetString(reader.GetOrdinal("event_end_date")),
        EventType = reader.GetString(reader.GetOrdinal("event_type")),
        EventLocation = reader.GetString(reader.GetOrdinal("event_location")),
        Requirements = reader.IsDBNull(reader.GetOrdinal("requirements")) ? null : reader.GetString(reader.GetOrdinal("requirements")),
        Status = reader.GetString(reader.GetOrdinal("status")),
        TotalAmount = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("total_amount"))),
        AdvancePaid = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("advance_paid"))),
        ItemsJson = reader.IsDBNull(reader.GetOrdinal("items_json")) ? null : reader.GetString(reader.GetOrdinal("items_json")),
        AdminNotes = reader.IsDBNull(reader.GetOrdinal("admin_notes")) ? null : reader.GetString(reader.GetOrdinal("admin_notes")),
        CreatedAt = reader.GetString(reader.GetOrdinal("created_at"))
    };
    #endregion

    #region Dashboard & Stats
    public DashboardSummary GetDashboardSummary()
    {
        using var conn = GetConnection();
        var summary = new DashboardSummary();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                COUNT(*) as Total,
                SUM(CASE WHEN status = 'Pending' THEN 1 ELSE 0 END) as Pending,
                SUM(CASE WHEN status = 'Confirmed' THEN 1 ELSE 0 END) as Confirmed,
                SUM(CASE WHEN status = 'Completed' THEN 1 ELSE 0 END) as Completed,
                SUM(CASE WHEN status = 'Cancelled' THEN 1 ELSE 0 END) as Cancelled,
                COALESCE(SUM(total_amount), 0) as TotalAmount,
                COALESCE(SUM(CASE WHEN status IN ('Confirmed', 'Completed') THEN total_amount ELSE 0 END), 0) as ConfirmedAmount
            FROM bookings;
        ";
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                summary.TotalBookings = reader.GetInt32(0);
                summary.PendingCount = reader.GetInt32(1);
                summary.ConfirmedCount = reader.GetInt32(2);
                summary.CompletedCount = reader.GetInt32(3);
                summary.CancelledCount = reader.GetInt32(4);
                summary.TotalEstimatedRevenue = Convert.ToDecimal(reader.GetDouble(5));
                summary.ConfirmedRevenue = Convert.ToDecimal(reader.GetDouble(6));
            }
        }

        var cmdItems = conn.CreateCommand();
        cmdItems.CommandText = "SELECT COUNT(*) FROM items WHERE is_active = 1;";
        summary.TotalInventoryItems = Convert.ToInt32(cmdItems.ExecuteScalar());

        var cmdPkgs = conn.CreateCommand();
        cmdPkgs.CommandText = "SELECT COUNT(*) FROM packages WHERE is_active = 1;";
        summary.TotalPackages = Convert.ToInt32(cmdPkgs.ExecuteScalar());

        var cmdRecent = conn.CreateCommand();
        cmdRecent.CommandText = "SELECT * FROM bookings ORDER BY id DESC LIMIT 5;";
        using (var reader = cmdRecent.ExecuteReader())
        {
            while (reader.Read())
            {
                summary.RecentBookings.Add(ReadBooking(reader));
            }
        }

        return summary;
    }
    #endregion

    #region Business Settings & Profile
    public BusinessSettings GetSettings()
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";
        using var reader = cmd.ExecuteReader();
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        var s = new BusinessSettings();
        if (dict.TryGetValue("BusinessName", out var bn)) s.BusinessName = bn;
        if (dict.TryGetValue("Tagline", out var tl)) s.Tagline = tl;
        if (dict.TryGetValue("Phone", out var ph)) s.Phone = ph;
        if (dict.TryGetValue("WhatsApp", out var wa)) s.WhatsApp = wa;
        if (dict.TryGetValue("Email", out var em)) s.Email = em;
        if (dict.TryGetValue("Address", out var ad)) s.Address = ad;
        if (dict.TryGetValue("OperatingHours", out var oh)) s.OperatingHours = oh;
        return s;
    }

    public void UpdateSettings(BusinessSettings settings)
    {
        using var conn = GetConnection();
        var pairs = new Dictionary<string, string>
        {
            { "BusinessName", settings.BusinessName },
            { "Tagline", settings.Tagline },
            { "Phone", settings.Phone },
            { "WhatsApp", settings.WhatsApp },
            { "Email", settings.Email },
            { "Address", settings.Address },
            { "OperatingHours", settings.OperatingHours }
        };

        foreach (var (k, v) in pairs)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@k, @v);";
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            cmd.ExecuteNonQuery();
        }
    }
    #endregion

    #region Auth & Password Management
    public bool ValidateAdmin(string username, string password)
    {
        using var conn = GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT password_hash, salt FROM admins WHERE username = @user;";
        cmd.Parameters.AddWithValue("@user", username);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;

        var storedHash = reader.GetString(0);
        var storedSalt = reader.GetString(1);

        var computedHash = HashPassword(password, storedSalt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(computedHash));
    }

    public bool ChangeAdminPassword(string username, string oldPassword, string newPassword)
    {
        if (!ValidateAdmin(username, oldPassword)) return false;

        using var conn = GetConnection();
        var newSalt = GenerateSalt();
        var newHash = HashPassword(newPassword, newSalt);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE admins SET password_hash = @hash, salt = @salt WHERE username = @user;";
        cmd.Parameters.AddWithValue("@hash", newHash);
        cmd.Parameters.AddWithValue("@salt", newSalt);
        cmd.Parameters.AddWithValue("@user", username);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 50000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }
    #endregion
}
