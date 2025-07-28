using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;    
using System.IO;                     
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProdCors", builder =>
    {
        builder.WithOrigins("https://acoomh.ro")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure JSON options to handle circular references
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Add Controllers
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Debug logging - show partial connection string for debugging
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Connection string configured: {!string.IsNullOrEmpty(connectionString)}");
if (!string.IsNullOrEmpty(connectionString))
{
    // Show first 80 chars to debug without exposing full password
    Console.WriteLine($"Connection string preview: {connectionString.Substring(0, Math.Min(80, connectionString.Length))}...");
}

// 🔧 FIX: Specify MySQL version manually to avoid AutoDetect connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));

var app = builder.Build();

// Apply migrations automatically on startup
Console.WriteLine("=== STARTING MIGRATION PROCESS ===");
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        Console.WriteLine("Testing database connection...");
        await dbContext.Database.CanConnectAsync();
        Console.WriteLine("✅ Database connection successful!");
        
        Console.WriteLine("Starting database migration...");
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("✅ Database migration completed successfully!");
        
        var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Console.WriteLine($"Applied migrations count: {migrations.Count()}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Migration failed: {ex.Message}");
    Console.WriteLine($"Full error: {ex}");
    // Don't crash the app - let it start
}
Console.WriteLine("=== MIGRATION PROCESS COMPLETED ===");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("ProdCors");
app.UseHttpsRedirection();
app.MapControllers();

// Health check endpoints
app.MapGet("/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName 
});

app.MapGet("/health/db", async (AppDbContext context) =>
{
    try
    {
        await context.Database.CanConnectAsync();
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        return Results.Ok(new { 
            status = "database connected", 
            migrationsApplied = migrations.Count(),
            timestamp = DateTime.UtcNow 
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

Console.WriteLine("🚀 Application starting...");
app.Urls.Add("http://0.0.0.0:8080");
// All endpoints updated to use AppDbContext
app.MapGet("/users", async (AppDbContext db) =>
    await db.Users.ToListAsync());

app.MapGet("/users/{id:int}", async (int id, AppDbContext db) =>
    await db.Users.FindAsync(id)
        is User user
        ? Results.Ok(user)
        : Results.NotFound());

app.MapPost("/users", async (HttpRequest req, AppDbContext db) =>
{
    var form = await req.ReadFormAsync();
    if (await db.Users.AnyAsync(u => u.Username == form["username"].ToString()) ||
        await db.Users.AnyAsync(u => u.Email == form["email"].ToString()))
    {
        return Results.Conflict(new {Error = "Username or email already exists!"});
    }

    var file = form.Files.GetFile("default");
    var user = new User
    {
        Username = form["username"].ToString(),
        FirstName = form["firstname"].ToString(),
        LastName = form["lastname"].ToString(),
        Email = form["email"].ToString(),
        Password = BCrypt.Net.BCrypt.HashPassword(form["password"].ToString()),
    };
    if (file != null && file.Length > 0)
    {
        using var ms = new MemoryStream();
        await file.OpenReadStream().CopyToAsync(ms);
        user.ProfileImage = ms.ToArray();
    }
    string? pfpUser = user.ProfileImage is not null
        ? Convert.ToBase64String(user.ProfileImage)
        : null;

    int lastId = await db.Users
        .OrderByDescending(u => u.Id)
        .Select(u => u.Id)
        .FirstOrDefaultAsync();
    var userResponse = new UserResponse
    {
        Id = lastId + 1,
        Username = user.Username,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        ProfileImage = pfpUser ?? string.Empty
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", userResponse);
});

app.MapPut("/users/{id:int}", async (int id, User input, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    user.Username = input.Username;
    user.FirstName = input.FirstName;
    user.LastName = input.LastName;
    user.Password = input.Password;
    user.Email = input.Email;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/login", async (LoginRequest req, AppDbContext db) =>
{
    // First, try to authenticate as a user
    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Username == req.Username || u.Email == req.Username);
    if (user != null && BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
    {
        string? pfpUser = user.ProfileImage is not null
            ? Convert.ToBase64String(user.ProfileImage)
            : null;

        var userResponse = new
        {
            Type = "User",
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            ProfileImage = pfpUser
        };
        return Results.Ok(userResponse);
    }

    // If user authentication fails, try to authenticate as a company
    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.Email == req.Username);
    if (company != null && BCrypt.Net.BCrypt.Verify(req.Password, company.Password))
    {
        var companyResponse = new
        {
            Type = "Company",
            Id = company.Id,
            Name = company.Name,
            Email = company.Email,
            Address = "", // Will come from locations
            Description = company.Description,
            Tags = new List<string>(), // Will come from locations
            Latitude = 0.0, // Will come from locations
            Longitude = 0.0, // Will come from locations
            Cui = company.Cui,
            Category = company.Category
        };
        return Results.Ok(companyResponse);
    }

    return Results.Unauthorized();
});

app.MapGet("/companies", async (AppDbContext db) =>
{
    List<Company> companies = await db.Companies.ToListAsync();
    List<CompanyResponse> companiesResponses = new List<CompanyResponse>();
    foreach (var c in companies)
    {
        var cr = new CompanyResponse
        {
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            Description = c.Description,
            Cui = c.Cui,
            Category = c.Category
        };
        companiesResponses.Add(cr);
    }
    return Results.Ok(companiesResponses);
});

app.MapPost("/companies", async (HttpRequest req, AppDbContext db) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        
        // Check if company with email already exists
        if (await db.Companies.AnyAsync(c => c.Email == form["email"].ToString()))
        {
            return Results.Conflict(new { Error = "Company with this email already exists!" });
        }

        // Check if company with CUI already exists
        if (int.TryParse(form["cui"].ToString(), out int cui) && await db.Companies.AnyAsync(c => c.Cui == cui))
        {
            return Results.Conflict(new { Error = "Company with this CUI already exists!" });
        }

        var company = new Company
        {
            Name = form["name"].ToString(),
            Email = form["email"].ToString(),
            Password = BCrypt.Net.BCrypt.HashPassword(form["password"].ToString()),
            Cui = cui,
            Category = form["category"].ToString(),
            Description = form.ContainsKey("description") ? form["description"].ToString() : ""
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var companyResponse = new
        {
            Type = "Company",
            Id = company.Id,
            Name = company.Name,
            Email = company.Email,
            Description = company.Description,
            Cui = company.Cui,
            Category = company.Category
        };

        return Results.Created($"/companies/{company.Id}", companyResponse);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error creating company: {ex.Message}");
    }
});

app.MapPut("changepfp", async (HttpRequest req, AppDbContext db) =>
{
    if (!req.HasFormContentType)
        return Results.BadRequest("Expected multipart form-data");

    var form = await req.ReadFormAsync();

    if (!form.TryGetValue("id", out var idValues) || !int.TryParse(idValues, out int userId))
        return Results.BadRequest("Missing or invalid 'id'");

    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
        return Results.BadRequest("Missing file");

    var user = await db.Users.FindAsync(userId);
    if (user == null)
        return Results.NotFound();

    using var ms = new MemoryStream();
    await file.OpenReadStream().CopyToAsync(ms);
    user.ProfileImage = ms.ToArray();

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/events", async (AppDbContext db) =>
{
    var events = await db.Events
        .Include(e => e.Company)
        .Where(e => e.IsActive)
        .ToListAsync();
        
    var eventResponses = events.Select(e => new EventResponse
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        Tags = string.IsNullOrEmpty(e.Tags) ? new List<string>() : e.Tags.Split(",").Select(t => t.Trim()).ToList(),
        Likes = db.Likes.Count(l => l.EventId == e.Id),
        Photo = e.Photo != null ? Convert.ToBase64String(e.Photo) : string.Empty,
        Company = e.Company?.Name ?? "Unknown",
        EventDate = e.EventDate,
        StartTime = e.StartTime.ToString(@"hh\:mm"),
        EndTime = e.EndTime.ToString(@"hh\:mm"),
        Address = e.Address,
        City = e.City,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt
    }).ToList();

    return Results.Ok(eventResponses);
});

app.MapPost("companyevents", async (HttpRequest req, AppDbContext db) =>
{
    var form = await req.ReadFormAsync();
    int companyId = int.Parse(form["id"].ToString());
    
    List<Event> events = await db.Events
        .Include(e => e.Company)
        .Where(e => e.CompanyId == companyId && e.IsActive)
        .ToListAsync();
    
    var eventResponses = events.Select(e => new EventResponse
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        Tags = string.IsNullOrEmpty(e.Tags) ? new List<string>() : e.Tags.Split(",").Select(t => t.Trim()).ToList(),
        Likes = db.Likes.Count(l => l.EventId == e.Id),
        Photo = e.Photo != null ? Convert.ToBase64String(e.Photo) : string.Empty,
        Company = e.Company?.Name ?? "Unknown",
        EventDate = e.EventDate,
        StartTime = e.StartTime.ToString(@"hh\:mm"),
        EndTime = e.EndTime.ToString(@"hh\:mm"),
        Address = e.Address,
        City = e.City,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt
    }).ToList();

    return Results.Ok(eventResponses);
});

// GET /events/{id} - Get single event by ID
app.MapGet("/events/{id}", async (int id, AppDbContext db) =>
{
    var eventItem = await db.Events
        .Include(e => e.Company)
        .FirstOrDefaultAsync(e => e.Id == id);
        
    if (eventItem == null)
    {
        return Results.NotFound();
    }

    var eventResponse = new EventResponse
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        Tags = string.IsNullOrEmpty(eventItem.Tags) ? new List<string>() : eventItem.Tags.Split(",").Select(t => t.Trim()).ToList(),
        Likes = await db.Likes.CountAsync(l => l.EventId == eventItem.Id),
        Photo = eventItem.Photo != null ? Convert.ToBase64String(eventItem.Photo) : string.Empty,
        Company = eventItem.Company?.Name ?? "Unknown",
        EventDate = eventItem.EventDate,
        StartTime = eventItem.StartTime.ToString(@"hh\:mm"),
        EndTime = eventItem.EndTime.ToString(@"hh\:mm"),
        Address = eventItem.Address,
        City = eventItem.City,
        Latitude = eventItem.Latitude,
        Longitude = eventItem.Longitude,
        IsActive = eventItem.IsActive,
        CreatedAt = eventItem.CreatedAt
    };

    return Results.Ok(eventResponse);
});

// POST /events/{id}/like - Like an event (requires userId in body)
app.MapPost("/events/{id}/like", async (int id, HttpRequest request, AppDbContext db) =>
{
    Console.WriteLine($"Like request received for event {id}");
    
    var form = await request.ReadFormAsync();
    Console.WriteLine($"Form data received: {string.Join(", ", form.Keys)}");
    
    if (!int.TryParse(form["userId"], out int userId))
    {
        Console.WriteLine($"Invalid userId in form data: {form["userId"]}");
        return Results.BadRequest(new { Error = "userId is required" });
    }
    
    Console.WriteLine($"User {userId} trying to like event {id}");
    
    var eventItem = await db.Events.FindAsync(id);
    if (eventItem == null)
    {
        Console.WriteLine($"Event {id} not found");
        return Results.NotFound(new { Error = "Event not found" });
    }
    
    var user = await db.Users.FindAsync(userId);
    if (user == null)
    {
        Console.WriteLine($"User {userId} not found");
        return Results.NotFound(new { Error = "User not found" });
    }

    // Check if user already liked this event
    var existingLike = await db.Likes
        .FirstOrDefaultAsync(l => l.EventId == id && l.UserId == userId);
        
    if (existingLike != null)
    {
        Console.WriteLine($"User {userId} has already liked event {id}");
        return Results.Conflict(new { Error = "User has already liked this event" });
    }

    // Create new like
    var like = new Like
    {
        EventId = id,
        UserId = userId,
        CreatedAt = DateTime.UtcNow
    };
    
    db.Likes.Add(like);
    eventItem.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    
    // Get current like count
    var likesCount = await db.Likes.CountAsync(l => l.EventId == id);
    
    Console.WriteLine($"Event {id} liked successfully by user {userId}. Total likes: {likesCount}");
    
    return Results.Ok(new { 
        EventId = eventItem.Id, 
        Likes = likesCount,
        IsLiked = true,
        Message = "Event liked successfully" 
    });
});

// POST /events/{id}/unlike - Unlike an event (requires userId in body)
app.MapPost("/events/{id}/unlike", async (int id, HttpRequest request, AppDbContext db) =>
{
    Console.WriteLine($"Unlike request received for event {id}");
    
    var form = await request.ReadFormAsync();
    Console.WriteLine($"Form data received: {string.Join(", ", form.Keys)}");
    
    if (!int.TryParse(form["userId"], out int userId))
    {
        Console.WriteLine($"Invalid userId in form data: {form["userId"]}");
        return Results.BadRequest(new { Error = "userId is required" });
    }
    
    Console.WriteLine($"User {userId} trying to unlike event {id}");
    
    var eventItem = await db.Events.FindAsync(id);
    if (eventItem == null)
    {
        Console.WriteLine($"Event {id} not found");
        return Results.NotFound(new { Error = "Event not found" });
    }

    // Find and remove the like
    var existingLike = await db.Likes
        .FirstOrDefaultAsync(l => l.EventId == id && l.UserId == userId);
        
    if (existingLike == null)
    {
        Console.WriteLine($"User {userId} has not liked event {id}");
        return Results.NotFound(new { Error = "User has not liked this event" });
    }

    db.Likes.Remove(existingLike);
    eventItem.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    
    // Get current like count
    var likesCount = await db.Likes.CountAsync(l => l.EventId == id);
    
    Console.WriteLine($"Event {id} unliked successfully by user {userId}. Total likes: {likesCount}");
    
    return Results.Ok(new { 
        EventId = eventItem.Id, 
        Likes = likesCount,
        IsLiked = false,
        Message = "Event unliked successfully" 
    });
});

// GET /events/{id}/like-status/{userId} - Check if user has liked an event
app.MapGet("/events/{id}/like-status/{userId}", async (int id, int userId, AppDbContext db) =>
{
    var eventItem = await db.Events.FindAsync(id);
    if (eventItem == null)
    {
        return Results.NotFound(new { Error = "Event not found" });
    }
    
    var user = await db.Users.FindAsync(userId);
    if (user == null)
    {
        return Results.NotFound(new { Error = "User not found" });
    }

    // Check if user has liked this event
    var isLiked = await db.Likes
        .AnyAsync(l => l.EventId == id && l.UserId == userId);
        
    // Get current like count
    var likesCount = await db.Likes.CountAsync(l => l.EventId == id);
    
    return Results.Ok(new { 
        EventId = id,
        UserId = userId,
        IsLiked = isLiked,
        Likes = likesCount
    });
});

// POST /events - Create new event
app.MapPost("/events", async (HttpRequest request, AppDbContext db) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        
        var companyId = int.Parse(form["companyId"].ToString());
        var company = await db.Companies.FindAsync(companyId);
        if (company == null)
        {
            return Results.BadRequest("Company not found");
        }

        // Handle image upload
        byte[] imageData = Array.Empty<byte>();
        if (form.Files.Count > 0)
        {
            // Handle file upload
            var file = form.Files[0];
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            imageData = memoryStream.ToArray();
        }
        else if (form.ContainsKey("photo") && !string.IsNullOrEmpty(form["photo"]))
        {
            // Handle base64 photo data
            try
            {
                var photoString = form["photo"].ToString();
                if (!string.IsNullOrEmpty(photoString))
                {
                    imageData = Convert.FromBase64String(photoString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting base64 photo: {ex.Message}");
            }
        }

        var newEvent = new Event
        {
            Title = form["title"].ToString(),
            Description = form["description"].ToString(),
            Tags = form["tags"].ToString() ?? "",
            CompanyId = companyId,
            Photo = imageData,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            
            // New enhanced event fields
            EventDate = DateTime.Parse(form["eventDate"].ToString()),
            StartTime = TimeSpan.Parse(form["startTime"].ToString()),
            EndTime = TimeSpan.Parse(form["endTime"].ToString()),
            IsActive = true,
            
            // Address fields
            Address = form["address"].ToString(),
            City = form["city"].ToString(),
            Latitude = double.TryParse(form["latitude"].ToString(), out var lat) ? lat : null,
            Longitude = double.TryParse(form["longitude"].ToString(), out var lng) ? lng : null
        };

        db.Events.Add(newEvent);
        await db.SaveChangesAsync();

        return Results.Ok(new { id = newEvent.Id, message = "Event created successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error creating event: {ex.Message}");
    }
});

// PUT /events/{id} - Update event
app.MapPut("/events/{id}", async (int id, HttpRequest request, AppDbContext db) =>
{
    try
    {
        var eventItem = await db.Events.FindAsync(id);
        if (eventItem == null)
        {
            return Results.NotFound();
        }

        var form = await request.ReadFormAsync();

        eventItem.Title = form["title"].ToString();
        eventItem.Description = form["description"].ToString();
        eventItem.Tags = form["tags"].ToString() ?? "";

        // Handle image upload if provided
        if (form.Files.Count > 0)
        {
            // Handle file upload
            var file = form.Files[0];
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            eventItem.Photo = memoryStream.ToArray();
        }
        else if (form.ContainsKey("photo") && !string.IsNullOrEmpty(form["photo"]))
        {
            // Handle base64 photo data
            try
            {
                var photoString = form["photo"].ToString();
                if (!string.IsNullOrEmpty(photoString))
                {
                    eventItem.Photo = Convert.FromBase64String(photoString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting base64 photo in update: {ex.Message}");
            }
        }

        eventItem.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Event updated successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Error updating event: {ex.Message}");
    }
});

// DELETE /events/{id} - Delete event
app.MapDelete("/events/{id}", async (int id, AppDbContext db) =>
{
    var eventItem = await db.Events.FindAsync(id);
    if (eventItem == null)
    {
        return Results.NotFound();
    }

    db.Events.Remove(eventItem);
    await db.SaveChangesAsync();
    
    return Results.Ok(new { message = "Event deleted successfully" });
});

// GET /events/{id}/attendees - Get event attendees (placeholder)
app.MapGet("/events/{id}/attendees", async (int id, AppDbContext db) =>
{
    // Placeholder implementation - will be implemented with proper attendee tracking
    var eventItem = await db.Events.FindAsync(id);
    if (eventItem == null)
    {
        return Results.NotFound();
    }

    // Return mock attendees for now
    var mockAttendees = new[]
    {
        new {
            id = 1,
            customerName = "John Doe",
            customerEmail = "john@example.com",
            registeredAt = DateTime.UtcNow.AddDays(-2),
            status = "confirmed"
        },
        new {
            id = 2,
            customerName = "Jane Smith", 
            customerEmail = "jane@example.com",
            registeredAt = DateTime.UtcNow.AddDays(-1),
            status = "pending"
        }
    };

    return Results.Ok(mockAttendees);
});

// OLD ENDPOINT - Deprecated: Company menu now handled per location
// Use /locations/{locationId}/menu instead
app.MapGet("/companies/{id}/menu", async (int id, AppDbContext db) =>
{
    // Redirect to first active location's menu if available
    var firstLocation = await db.Locations
        .Where(l => l.CompanyId == id && l.IsActive && l.MenuData.Length > 0)
        .FirstOrDefaultAsync();
        
    if (firstLocation == null)
    {
        return Results.NotFound("Meniu inexistent pentru această companie");
    }

    return Results.File(firstLocation.MenuData, "application/pdf", firstLocation.MenuName);
});

// OLD ENDPOINT - Deprecated: Company menu upload now handled per location
// Use POST /locations/{locationId}/upload-menu instead
app.MapPost("/companies/{id}/upload-menu", (int id, HttpRequest request, AppDbContext db) =>
{
    return Results.BadRequest(new { 
        message = "This endpoint is deprecated. Please use location-specific menu upload.",
        newEndpoint = "/locations/{locationId}/upload-menu",
        note = "Companies now manage menus per location, not globally."
    });
});

// ==================== LOCATION MANAGEMENT ENDPOINTS ====================

// Get all locations for a company
app.MapGet("/companies/{companyId}/locations", async (int companyId, AppDbContext db) =>
{
    var locations = await db.Locations
        .Where(l => l.CompanyId == companyId && l.IsActive)
        .ToListAsync();
        
    var result = locations.Select(l => new
    {
        l.Id,
        l.Name,
        l.Address,
        l.Category,
        l.Latitude,
        l.Longitude,
        Tags = string.IsNullOrEmpty(l.Tags) ? new string[0] : l.Tags.Split(',').Select(t => t.Trim()).ToArray(),
        Photo = Convert.ToBase64String(l.Photo),
        MenuName = l.MenuName,
        HasMenu = l.MenuData.Length > 0,
        l.CreatedAt,
        l.UpdatedAt
    }).ToList();
        
    return Results.Ok(result);
});

// Get all locations across all companies (for main app)
app.MapGet("/locations", async (AppDbContext db) =>
{
    var locations = await db.Locations
        .Include(l => l.Company)
        .Where(l => l.IsActive)
        .ToListAsync();
        
    var result = locations.Select(l => new
    {
        l.Id,
        l.Name,
        l.Address,
        l.Category,
        l.Latitude,
        l.Longitude,
        Tags = string.IsNullOrEmpty(l.Tags) ? new string[0] : l.Tags.Split(',').Select(t => t.Trim()).ToArray(),
        Photo = Convert.ToBase64String(l.Photo),
        MenuName = l.MenuName,
        HasMenu = l.MenuData.Length > 0,
        Company = new
        {
            l.Company.Id,
            l.Company.Name,
            l.Company.Category,
            l.Company.Description
        },
        l.CreatedAt,
        l.UpdatedAt
    }).ToList();
        
    return Results.Ok(result);
});

// Get a specific location
app.MapGet("/locations/{id}", async (int id, AppDbContext db) =>
{
    var location = await db.Locations
        .Include(l => l.Company)
        .Where(l => l.Id == id && l.IsActive)
        .FirstOrDefaultAsync();
        
    if (location is null)
        return Results.NotFound();
        
    var result = new
    {
        location.Id,
        location.Name,
        location.Address,
        location.Latitude,
        location.Longitude,
        Tags = string.IsNullOrEmpty(location.Tags) ? new string[0] : location.Tags.Split(',').Select(t => t.Trim()).ToArray(),
        Photo = Convert.ToBase64String(location.Photo),
        MenuName = location.MenuName,
        HasMenu = location.MenuData.Length > 0,
        Company = new
        {
            location.Company.Id,
            location.Company.Name,
            location.Company.Category
        },
        location.CreatedAt,
        location.UpdatedAt
    };
        
    return Results.Ok(result);
});

// Create a new location
app.MapPost("/companies/{companyId}/locations", async (int companyId, HttpRequest req, AppDbContext db) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        
        // Check if company exists
        var company = await db.Companies.FindAsync(companyId);
        if (company is null)
        {
            return Results.NotFound(new { Error = "Company not found" });
        }
        
        // Check if location name already exists for this company (active or inactive)
        var existingLocation = await db.Locations
            .AnyAsync(l => l.CompanyId == companyId && l.Name == form["name"].ToString());
        if (existingLocation)
        {
            return Results.Conflict(new { Error = "Location with this name already exists for this company. Please choose a different name." });
        }

        var photoFile = form.Files.GetFile("photo");
        var menuFile = form.Files.GetFile("menu");
        
        var location = new Location
        {
            CompanyId = companyId,
            Name = form["name"].ToString(),
            Address = form["address"].ToString(),
            Category = form["category"].ToString(),
            Latitude = double.Parse(form["latitude"].ToString()),
            Longitude = double.Parse(form["longitude"].ToString()),
            Tags = form["tags"].ToString()
        };

        // Handle photo upload
        if (photoFile != null && photoFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await photoFile.OpenReadStream().CopyToAsync(ms);
            location.Photo = ms.ToArray();
        }

        // Handle menu upload
        if (menuFile != null && menuFile.Length > 0)
        {
            location.MenuName = menuFile.FileName;
            using var ms = new MemoryStream();
            await menuFile.OpenReadStream().CopyToAsync(ms);
            location.MenuData = ms.ToArray();
            location.HasMenu = true;
        }

        db.Locations.Add(location);
        await db.SaveChangesAsync();

        var response = new
        {
            location.Id,
            location.Name,
            location.Address,
            location.Category,
            location.Latitude,
            location.Longitude,
            Tags = string.IsNullOrEmpty(location.Tags) ? new string[0] : location.Tags.Split(',').Select(t => t.Trim()).ToArray(),
            Photo = Convert.ToBase64String(location.Photo),
            location.MenuName,
            location.HasMenu
        };

        return Results.Created($"/locations/{location.Id}", response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error creating location: {ex.Message}");
    }
});

// Update a location
app.MapPut("/locations/{id}", async (int id, HttpRequest req, AppDbContext db) =>
{
    try
    {
        var location = await db.Locations.FindAsync(id);
        if (location is null || !location.IsActive)
        {
            return Results.NotFound();
        }

        var form = await req.ReadFormAsync();
        
        // Check if another location with the same name exists for this company
        var newName = form["name"].ToString();
        var existingLocation = await db.Locations
            .AnyAsync(l => l.CompanyId == location.CompanyId && l.Name == newName && l.Id != id);
        if (existingLocation)
        {
            return Results.Conflict(new { Error = "Another location with this name already exists for this company. Please choose a different name." });
        }
        
        location.Name = newName;
        location.Address = form["address"].ToString();
        location.Latitude = double.Parse(form["latitude"].ToString());
        location.Longitude = double.Parse(form["longitude"].ToString());
        location.Tags = form["tags"].ToString();
        location.UpdatedAt = DateTime.UtcNow;

        var photoFile = form.Files.GetFile("photo");
        if (photoFile != null && photoFile.Length > 0)
        {
            using var ms = new MemoryStream();
            await photoFile.OpenReadStream().CopyToAsync(ms);
            location.Photo = ms.ToArray();
        }
        else if (form.ContainsKey("photo") && !string.IsNullOrEmpty(form["photo"]))
        {
            // Handle base64 photo data
            var photoString = form["photo"].ToString();
            if (photoString.StartsWith("data:image"))
            {
                var base64Data = photoString.Substring(photoString.IndexOf(',') + 1);
                location.Photo = Convert.FromBase64String(base64Data);
            }
        }

        var menuFile = form.Files.GetFile("menu");
        if (menuFile != null && menuFile.Length > 0)
        {
            location.MenuName = menuFile.FileName;
            using var ms = new MemoryStream();
            await menuFile.OpenReadStream().CopyToAsync(ms);
            location.MenuData = ms.ToArray();
            location.HasMenu = true;
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error updating location: {ex.Message}");
    }
});

// Delete a location (soft delete)
app.MapDelete("/locations/{id}", async (int id, AppDbContext db) =>
{
    var location = await db.Locations.FindAsync(id);
    if (location is null)
    {
        return Results.NotFound();
    }

    location.IsActive = false;
    location.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    
    return Results.NoContent();
});

// Get location hours
app.MapGet("/locations/{locationId}/hours", async (int locationId, AppDbContext db) =>
{
    var hours = await db.LocationHours
        .Where(h => h.LocationId == locationId)
        .Select(h => new
        {
            h.Id,
            DayOfWeek = h.DayOfWeek.ToString(),
            h.IsClosed,
            OpenTime = h.OpenTime.HasValue ? h.OpenTime.Value.ToString(@"hh\:mm") : "",
            CloseTime = h.CloseTime.HasValue ? h.CloseTime.Value.ToString(@"hh\:mm") : ""
        })
        .ToListAsync();
    
    return Results.Ok(hours);
});

// Set location hours
app.MapPost("/locations/{locationId}/hours", async (int locationId, HttpRequest req, AppDbContext db) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        
        // Check if location exists
        var location = await db.Locations.FindAsync(locationId);
        if (location is null || !location.IsActive)
        {
            return Results.NotFound(new { Error = "Location not found" });
        }

        // Remove existing hours for this location
        var existingHours = await db.LocationHours
            .Where(h => h.LocationId == locationId)
            .ToListAsync();
        db.LocationHours.RemoveRange(existingHours);

        // Parse and add new hours
        for (int i = 0; i < 7; i++)
        {
            var dayOfWeek = (DayOfWeek)i;
            var isClosed = bool.Parse(form[$"day_{i}_closed"].ToString());
            
            var locationHour = new LocationHour
            {
                LocationId = locationId,
                DayOfWeek = dayOfWeek,
                IsClosed = isClosed
            };

            if (!isClosed)
            {
                locationHour.OpenTime = TimeSpan.Parse(form[$"day_{i}_open"].ToString());
                locationHour.CloseTime = TimeSpan.Parse(form[$"day_{i}_close"].ToString());
            }

            db.LocationHours.Add(locationHour);
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { Message = "Location hours updated successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error updating location hours: {ex.Message}");
    }
});

// Get location menu
app.MapGet("/locations/{id}/menu", async (int id, AppDbContext db) =>
{
    var location = await db.Locations.FindAsync(id);
    if (location is null || !location.IsActive || location.MenuData.Length == 0)
    {
        return Results.NotFound();
    }

    return Results.File(location.MenuData, "application/pdf", location.MenuName);
});

// ==================== END LOCATION ENDPOINTS ====================

// Missing endpoints for restaurant app
app.MapPost("/get-reservations", async (HttpRequest req, AppDbContext db) =>
{
    if (!req.HasFormContentType)
        return Results.BadRequest("Expected multipart form-data");

    var form = await req.ReadFormAsync();
    
    if (!form.TryGetValue("company_id", out var companyIdValues) || 
        !int.TryParse(companyIdValues, out int companyId))
        return Results.BadRequest("Missing or invalid 'company_id'");

    try
    {
        var reservations = await db.Reservations
            .Include(r => r.Location)
            .Where(r => r.Location.CompanyId == companyId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenBy(r => r.ReservationTime)
            .ToListAsync();

        var reservationResponses = reservations.Select(r => new
        {
            id = r.Id,
            customerName = r.CustomerName,
            customerEmail = r.CustomerEmail,
            customerPhone = r.CustomerPhone,
            reservationDate = r.ReservationDate.ToString("yyyy-MM-dd"),
            reservationTime = r.ReservationTime.ToString(@"hh\:mm"),
            numberOfPeople = r.NumberOfPeople,
            specialRequests = r.SpecialRequests,
            status = r.Status.ToString(),
            createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            updatedAt = r.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            confirmedAt = r.ConfirmedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            completedAt = r.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            canceledAt = r.CanceledAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            cancellationReason = r.CancellationReason,
            notes = r.Notes
        }).ToList();

        return Results.Ok(reservationResponses);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching reservations: {ex.Message}");
    }
});

app.MapPost("/get-stats", async (HttpRequest req, AppDbContext db) =>
{
    if (!req.HasFormContentType)
        return Results.BadRequest("Expected multipart form-data");

    var form = await req.ReadFormAsync();
    
    if (!form.TryGetValue("company_id", out var companyIdValues) || 
        !int.TryParse(companyIdValues, out int companyId))
        return Results.BadRequest("Missing or invalid 'company_id'");

    try
    {
        var company = await db.Companies.FindAsync(companyId);
        if (company == null)
            return Results.NotFound("Company not found");

        // Calculate current month stats
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;

        // Get reservations for current month
        var currentMonthReservations = await db.Reservations
            .Include(r => r.Location)
            .Where(r => r.Location.CompanyId == companyId && 
                       r.ReservationDate.Month == currentMonth && 
                       r.ReservationDate.Year == currentYear)
            .ToListAsync();

        // Get all-time reservations for comparison
        var allReservations = await db.Reservations
            .Include(r => r.Location)
            .Where(r => r.Location.CompanyId == companyId)
            .ToListAsync();

        // Calculate stats
        var totalReservations = allReservations.Count;
        var currentMonthCount = currentMonthReservations.Count;
        var confirmedReservations = currentMonthReservations.Count(r => r.Status == ReservationStatus.Confirmed);
        var completedReservations = currentMonthReservations.Count(r => r.Status == ReservationStatus.Completed);
        var cancelledReservations = currentMonthReservations.Count(r => r.Status == ReservationStatus.Canceled);

        // Calculate previous month for comparison
        var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
        var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
        
        var previousMonthCount = await db.Reservations
            .Include(r => r.Location)
            .Where(r => r.Location.CompanyId == companyId && 
                       r.ReservationDate.Month == previousMonth && 
                       r.ReservationDate.Year == previousYear)
            .CountAsync();

        // Calculate growth percentage
        var reservationGrowth = previousMonthCount > 0 
            ? ((double)(currentMonthCount - previousMonthCount) / previousMonthCount) * 100 
            : currentMonthCount > 0 ? 100.0 : 0.0;

        // Calculate average people per reservation
        var avgPeoplePerReservation = currentMonthReservations.Any() 
            ? currentMonthReservations.Average(r => r.NumberOfPeople) 
            : 0;

        // Generate response data
        var statsResponse = new
        {
            totalReservations = totalReservations,
            currentMonthReservations = currentMonthCount,
            confirmedReservations = confirmedReservations,
            completedReservations = completedReservations,
            cancelledReservations = cancelledReservations,
            reservationGrowth = Math.Round(reservationGrowth, 1),
            averagePeoplePerReservation = Math.Round(avgPeoplePerReservation, 1),
            // Mock data for other stats that may be tracked later
            views = totalReservations * 12, // Estimated views based on reservations
            directions = (int)(totalReservations * 0.7), // Estimated directions
            callsToAction = currentMonthCount,
            period = $"{DateTime.Now:MMMM yyyy}"
        };

        return Results.Ok(statsResponse);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching stats: {ex.Message}");
    }
});

// Reservation endpoints
app.MapPost("/reservation", async (HttpRequest req, AppDbContext db) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        
        var reservation = new Reservation
        {
            CustomerName = form["customerName"].ToString(),
            CustomerEmail = form["customerEmail"].ToString(),
            CustomerPhone = form["customerPhone"].ToString(),
            ReservationDate = DateTime.Parse(form["reservationDate"].ToString()),
            ReservationTime = TimeSpan.Parse(form["reservationTime"].ToString()),
            NumberOfPeople = int.Parse(form["numberOfPeople"].ToString()),
            SpecialRequests = form["specialRequests"].ToString(),
            LocationId = int.Parse(form["locationId"].ToString()),
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Handle optional UserId (for users from main app)
        if (form.ContainsKey("userId") && !string.IsNullOrEmpty(form["userId"]))
        {
            reservation.UserId = int.Parse(form["userId"].ToString());
        }

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        return Results.Created($"/reservation/{reservation.Id}", reservation);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error creating reservation: {ex.Message}");
    }
});

app.MapGet("/reservation/company/{companyId}", async (AppDbContext db, int companyId) =>
{
    try
    {
        var reservations = await db.Reservations
            .Include(r => r.Location)
            .Where(r => r.Location.CompanyId == companyId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenBy(r => r.ReservationTime)
            .Select(r => new
            {
                r.Id,
                r.CustomerName,
                r.CustomerEmail,
                r.CustomerPhone,
                r.ReservationDate,
                r.ReservationTime,
                r.NumberOfPeople,
                r.Status,
                r.SpecialRequests,
                r.Notes,
                r.CancellationReason,
                r.CreatedAt,
                r.UpdatedAt,
                r.LocationId,
                LocationName = r.Location.Name
            })
            .ToListAsync();

        return Results.Ok(reservations);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching reservations: {ex.Message}");
    }
});

app.MapGet("/reservation/user/{userId}", async (AppDbContext db, int userId) =>
{
    try
    {
        var reservations = await db.Reservations
            .Include(r => r.Location)
                .ThenInclude(l => l.Company)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenBy(r => r.ReservationTime)
            .Select(r => new ReservationResponse
            {
                Id = r.Id,
                CustomerName = r.CustomerName,
                CustomerEmail = r.CustomerEmail,
                CustomerPhone = r.CustomerPhone,
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                NumberOfPeople = r.NumberOfPeople,
                SpecialRequests = r.SpecialRequests,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                ConfirmedAt = r.ConfirmedAt,
                CompletedAt = r.CompletedAt,
                CanceledAt = r.CanceledAt,
                CancellationReason = r.CancellationReason,
                Notes = r.Notes,
                LocationId = r.LocationId,
                LocationName = r.Location.Name,
                CompanyId = r.Location.CompanyId,
                CompanyName = r.Location.Company.Name,
                UserId = r.UserId
            })
            .ToListAsync();

        return Results.Ok(reservations);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching user reservations: {ex.Message}");
    }
});

app.MapPut("/reservation/{id}", async (int id, HttpRequest req, AppDbContext db) =>
{
    try
    {
        var form = await req.ReadFormAsync();
        var reservation = await db.Reservations.FindAsync(id);
        
        if (reservation == null)
            return Results.NotFound("Reservation not found");

        if (form.ContainsKey("status"))
        {
            reservation.Status = Enum.Parse<ReservationStatus>(form["status"].ToString());
            reservation.UpdatedAt = DateTime.UtcNow;
            
            if (reservation.Status == ReservationStatus.Confirmed)
                reservation.ConfirmedAt = DateTime.UtcNow;
            else if (reservation.Status == ReservationStatus.Completed)
                reservation.CompletedAt = DateTime.UtcNow;
            else if (reservation.Status == ReservationStatus.Canceled)
                reservation.CanceledAt = DateTime.UtcNow;
        }

        if (form.ContainsKey("notes"))
            reservation.Notes = form["notes"].ToString();

        if (form.ContainsKey("cancellationReason"))
            reservation.CancellationReason = form["cancellationReason"].ToString();

        await db.SaveChangesAsync();
        return Results.Ok(reservation);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error updating reservation: {ex.Message}");
    }
});

// GET: reservation/{id}
app.MapGet("/reservation/{id}", async (int id, AppDbContext db) =>
{
    try
    {
        var reservation = await db.Reservations
            .Include(r => r.Location)
                .ThenInclude(l => l.Company)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            return Results.NotFound("Reservation not found");
        }

        var response = new ReservationResponse
        {
            Id = reservation.Id,
            CustomerName = reservation.CustomerName,
            CustomerEmail = reservation.CustomerEmail,
            CustomerPhone = reservation.CustomerPhone,
            ReservationDate = reservation.ReservationDate,
            ReservationTime = reservation.ReservationTime,
            NumberOfPeople = reservation.NumberOfPeople,
            SpecialRequests = reservation.SpecialRequests,
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt,
            ConfirmedAt = reservation.ConfirmedAt,
            CompletedAt = reservation.CompletedAt,
            CanceledAt = reservation.CanceledAt,
            CancellationReason = reservation.CancellationReason,
            Notes = reservation.Notes,
            LocationId = reservation.LocationId,
            LocationName = reservation.Location.Name,
            UserId = reservation.UserId
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching reservation: {ex.Message}");
    }
});

// GET: locations/{locationId}/reservations - Get all reservations for a specific location
app.MapGet("/locations/{locationId}/reservations", async (int locationId, AppDbContext db) =>
{
    try
    {
        // Verify location exists
        var location = await db.Locations.FindAsync(locationId);
        if (location == null)
        {
            return Results.NotFound("Location not found");
        }

        var reservations = await db.Reservations
            .Where(r => r.LocationId == locationId)
            .OrderByDescending(r => r.ReservationDate)
            .ThenByDescending(r => r.ReservationTime)
            .Select(r => new
            {
                r.Id,
                CustomerName = r.CustomerName,
                CustomerEmail = r.CustomerEmail,
                ReservationDate = r.ReservationDate.ToString("yyyy-MM-dd"),
                TimeSlot = r.ReservationTime.ToString(@"hh\:mm"),
                NumberOfPeople = r.NumberOfPeople,
                Status = r.Status.ToString().ToLower(),
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return Results.Ok(reservations);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching location reservations: {ex.Message}");
    }
});

// GET: reservation/available-times/{locationId}?date=...
app.MapGet("/reservation/available-times/{locationId}", async (int locationId, DateTime date, AppDbContext db) =>
{
    try
    {
        var locationHours = await db.LocationHours
            .FirstOrDefaultAsync(lh => lh.LocationId == locationId && 
                                      lh.DayOfWeek == date.DayOfWeek);

        if (locationHours == null || locationHours.IsClosed)
        {
            return Results.BadRequest("Location is closed on this day");
        }

        if (!locationHours.OpenTime.HasValue || !locationHours.CloseTime.HasValue)
        {
            return Results.BadRequest("Business hours not configured for this day");
        }

        // Generate available times between open and close
        var availableTimes = new List<TimeSpan>();
        var openTime = locationHours.OpenTime.Value;
        var closeTime = locationHours.CloseTime.Value;

        // Handle overnight operations (like 22:00 to 06:00)
        if (closeTime < openTime)
        {
            // From open time to midnight
            for (var time = openTime; time < TimeSpan.FromHours(24); time = time.Add(TimeSpan.FromMinutes(30)))
            {
                availableTimes.Add(time);
            }
            // From midnight to close time
            for (var time = TimeSpan.Zero; time < closeTime; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                availableTimes.Add(time);
            }
        }
        else
        {
            // Normal operation (like 09:00 to 22:00)
            for (var time = openTime; time < closeTime; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                availableTimes.Add(time);
            }
        }

        // Get the location to find its company
        var location = await db.Locations.FindAsync(locationId);
        if (location == null)
        {
            return Results.NotFound("Location not found");
        }

        // Filter out times that are already reserved (optional, based on business logic)
        var existingReservations = await db.Reservations
            .Where(r => r.LocationId == locationId && 
                       r.ReservationDate.Date == date.Date &&
                       r.Status != ReservationStatus.Canceled)
            .Select(r => r.ReservationTime)
            .ToListAsync();

        // Remove times that are already taken (assuming one reservation per time slot)
        availableTimes = availableTimes.Where(t => !existingReservations.Contains(t)).ToList();

        return Results.Ok(availableTimes);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error fetching available times: {ex.Message}");
    }
});

// Temporary debug endpoint to check company password status
app.MapGet("/debug/company", async (string email, AppDbContext db) =>
{
    var company = await db.Companies.FirstOrDefaultAsync(c => c.Email == email);
    if (company == null)
        return Results.NotFound("Company not found");
    
    return Results.Ok(new {
        Id = company.Id,
        Name = company.Name,
        Email = company.Email,
        HasPassword = !string.IsNullOrEmpty(company.Password),
        PasswordLength = company.Password?.Length ?? 0,
        PasswordStartsWith = company.Password?.Length > 0 ? company.Password.Substring(0, Math.Min(10, company.Password.Length)) : "",
        IsPasswordBCryptHashed = company.Password?.StartsWith("$2") ?? false
    });
});

app.Run();