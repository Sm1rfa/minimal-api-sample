using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

string connString = builder.Configuration.GetConnectionString("TodoDb");

builder.Services.AddDbContext<TodoDbContext>(opt => opt.UseSqlite(connString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dataContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    dataContext.Database.Migrate();
}

app.MapGet("/", () => "Hello World!");

app.MapGet("/speditor", async (TodoDbContext db) =>
{
    var data = await db.Speditors.Include(x => x.RetailerInfo).ToListAsync();

    return Results.Json(data, options);
});

app.MapGet("/retailer", async ([FromQuery] string country, TodoDbContext db) =>
    await db.Retailers
        .Include(x => x.Speditors)
        .Where(t => t.Country.Equals(country)).ToListAsync());

app.MapPost("/speditor", async ([FromBody] Speditor tc, TodoDbContext db) =>
{
    db.Speditors.Add(tc);
    await db.SaveChangesAsync();

    return Results.Created($"/speditor/{tc.Id}", tc);
});

app.MapPost("/retailer", async ([FromBody] Retailer ti, TodoDbContext db) =>
{
    ti.CreatedOn = DateTime.Parse(ti.CreatedOn.ToString("dd/MM/yyyy"));

    db.Retailers.Add(ti);
    await db.SaveChangesAsync();

    return Results.Created($"/retailer/{ti.Id}", ti);
});

app.MapPut("/speditor/{id}", async (int id, Speditor inputTc, TodoDbContext db) =>
{
    var todo = await db.Speditors.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.MaxItems = inputTc.MaxItems;
    todo.MinItems = inputTc.MinItems;
    todo.MaxDistance = inputTc.MaxDistance;
    todo.NumberOfVans = inputTc.NumberOfVans;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapDelete("/retailer/{id}", async (int id, TodoDbContext db) =>
{
    if (await db.Retailers
        .Include(x => x.Speditors)
        .Where(y => y.Id == id)
        .FirstOrDefaultAsync() is Retailer ti)
    {
        db.Retailers.Remove(ti);
        await db.SaveChangesAsync();
        return Results.Ok(ti);
    }

    return Results.NotFound();
});

app.Run();

class Speditor
{
    public int Id { get; set; }
    public int MinItems { get; set; }
    public int MaxItems { get; set; }
    public int MinDistance { get; set; }
    public int MaxDistance { get; set; }
    public int NumberOfVans { get; set; }
    public int RetailerId { get; set; }
    public Retailer? RetailerInfo { get; set; }
}

class Retailer
{
    public int Id { get; set; }
    public string? Country { get; set; }
    public string? Company { get; set; }
    public DateTime CreatedOn { get; set; }
    public bool IsActive { get; set; }
    public List<Speditor>? Speditors { get; set; }
}

class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions options) 
        : base(options)
    {
    }

    public DbSet<Speditor> Speditors => Set<Speditor>();
    public DbSet<Retailer> Retailers => Set<Retailer>();
}