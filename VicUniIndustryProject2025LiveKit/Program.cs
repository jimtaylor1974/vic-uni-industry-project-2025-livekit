var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure Swagger for development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --------------------------------------------------------------------
// In-memory data store
// --------------------------------------------------------------------
var employees = new List<Employee>
{
    new Employee(1, "Alice", "alice@example.com"),
    new Employee(2, "Bob", "bob@example.com"),
    new Employee(3, "Charlie", "charlie@example.com")
};

var visitors = new List<Visitor>();

// This can represent companies whose contractors are approved
var approvedContractorCompanies = new List<string>
{
    "Acme Plumbing",
    "XYZ Electrical",
    "Best Builders"
};

// Simple incremental ID generator for Visitors
var nextVisitorId = 1;

// --------------------------------------------------------------------
// Sample "WeatherForecast" endpoint (kept from template)
// --------------------------------------------------------------------
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm",
    "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// --------------------------------------------------------------------
// New Visitor Management Endpoints
// --------------------------------------------------------------------

app.MapGet("/employees", () =>
{
    return employees;
})
.WithName("GetEmployees")
.WithOpenApi();

app.MapGet("/visitors", () =>
{
    return visitors;
})
.WithName("GetAllVisitors")
.WithOpenApi();

app.MapGet("/visitors/on-site", () =>
{
    return visitors.Where(v => v.IsOnSite).ToList();
})
.WithName("GetVisitorsOnSite")
.WithOpenApi();

app.MapPost("/visitors/arrive-meeting", (VisitorArriveMeetingRequest request) =>
{
    // Check if the employee exists
    var employee = employees.FirstOrDefault(e =>
        e.Name.Equals(request.MeetingWith, StringComparison.OrdinalIgnoreCase));

    if (employee == null)
    {
        return Results.NotFound($"Employee '{request.MeetingWith}' not found.");
    }

    // Create a new visitor record
    var visitor = new Visitor(
        Id: nextVisitorId++,
        Name: request.VisitorName,
        ArrivalTime: DateTime.UtcNow,
        IsOnSite: true,
        Reason: "Meeting",
        MeetingWith: employee.Name,
        ContractorCompany: null,
        DepartureTime: null
    );
    visitors.Add(visitor);

    // In a real system you would notify the employee here
    var message = $"Visitor '{request.VisitorName}' has arrived for meeting with '{employee.Name}'. Notification sent.";
    return Results.Ok(message);
})
.WithName("VisitorArriveMeeting")
.WithOpenApi();

app.MapPost("/visitors/arrive-courier", (VisitorArriveCourierRequest request) =>
{
    // Create a new visitor (courier)
    var visitor = new Visitor(
        Id: nextVisitorId++,
        Name: request.CourierName,
        ArrivalTime: DateTime.UtcNow,
        IsOnSite: true,
        Reason: "Courier",
        MeetingWith: null,
        ContractorCompany: null,
        DepartureTime: null
    );
    visitors.Add(visitor);

    // In a real system, you'd notify reception via email/alert, etc.
    var message = $"Courier '{request.CourierName}' has arrived. Reception notified. " +
                  "Please leave the parcel at the designated location.";
    return Results.Ok(message);
})
.WithName("VisitorArriveCourier")
.WithOpenApi();

app.MapPost("/visitors/arrive-contractor", (VisitorArriveContractorRequest request) =>
{
    // Check if the contractor's company is in the approved list
    var isApproved = approvedContractorCompanies.Any(c =>
        c.Equals(request.Company, StringComparison.OrdinalIgnoreCase));

    if (!isApproved)
    {
        return Results.BadRequest($"Contractor's company '{request.Company}' is not approved.");
    }

    // Create a new visitor (contractor)
    var visitor = new Visitor(
        Id: nextVisitorId++,
        Name: request.VisitorName,
        ArrivalTime: DateTime.UtcNow,
        IsOnSite: true,
        Reason: "Contractor",
        MeetingWith: null,
        ContractorCompany: request.Company,
        DepartureTime: null
    );
    visitors.Add(visitor);

    // Notify reception logic (pseudo)
    var message = $"Contractor '{request.VisitorName}' from '{request.Company}' has arrived. Reception notified.";
    return Results.Ok(message);
})
.WithName("VisitorArriveContractor")
.WithOpenApi();

app.MapPost("/visitors/sign-out", (VisitorSignOutRequest request) =>
{
    var visitor = visitors.FirstOrDefault(v => v.Id == request.VisitorId);

    if (visitor == null)
    {
        return Results.NotFound($"Visitor with ID {request.VisitorId} not found.");
    }

    if (!visitor.IsOnSite)
    {
        return Results.Ok($"Visitor '{visitor.Name}' (ID {visitor.Id}) is already signed out.");
    }

    // Update visitor's record to mark them as signed out
    visitors.Remove(visitor);
    var updatedVisitor = visitor with
    {
        IsOnSite = false,
        DepartureTime = DateTime.UtcNow
    };
    visitors.Add(updatedVisitor);

    var message = $"Visitor '{visitor.Name}' (ID {visitor.Id}) has been signed out.";
    return Results.Ok(message);
})
.WithName("VisitorSignOut")
.WithOpenApi();

app.Run();

// --------------------------------------------------------------------
// Supporting WeatherForecast record (from template)
// --------------------------------------------------------------------
internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// --------------------------------------------------------------------
// Entities
// --------------------------------------------------------------------
record Employee(int Id, string Name, string Email);

/// <summary>
/// Represents a visitor's record in memory.
/// </summary>
record Visitor(
    int Id,
    string Name,
    DateTime ArrivalTime,
    bool IsOnSite,
    string Reason,             // e.g., "Meeting", "Courier", "Contractor"
    string? MeetingWith,       // If reason is "Meeting", store employee's name
    string? ContractorCompany, // If reason is "Contractor"
    DateTime? DepartureTime    // Track when visitor leaves
);

// --------------------------------------------------------------------
// Request DTOs for inbound data
// --------------------------------------------------------------------

/// <summary>
/// Request to sign in a visitor who has come for a meeting.
/// </summary>
record VisitorArriveMeetingRequest(
    string VisitorName,
    string MeetingWith
);

/// <summary>
/// Request to sign in a courier.
/// </summary>
record VisitorArriveCourierRequest(
    string CourierName
);

/// <summary>
/// Request to sign in a contractor.
/// </summary>
record VisitorArriveContractorRequest(
    string VisitorName,
    string Company
);

/// <summary>
/// Request to sign out an existing visitor by ID.
/// </summary>
record VisitorSignOutRequest(
    int VisitorId
);