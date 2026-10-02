using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.Hubs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Add DbContext with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=pos_edge.db";
builder.Services.AddDbContext<PosDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. Add SignalR for Realtime KDS & Table synchronization
builder.Services.AddSignalR();

// 3. Add Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 4. Configure CORS for Frontend (Vite on port 5173, preview on 4173, etc.)
var rawOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:5173", "http://localhost:4173", "http://localhost:3000" };

var allowedOrigins = rawOrigins.Select(o => o.TrimEnd('/')).ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PosFrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowed(origin => 
              {
                  if (string.IsNullOrEmpty(origin)) return false;
                  var uri = new Uri(origin);
                  return uri.Host == "localhost" || uri.Host == "127.0.0.1" || allowedOrigins.Contains(origin.TrimEnd('/'));
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5. OpenAPI & Scalar API reference
builder.Services.AddOpenApi();

var app = builder.Build();

// 6. Initialize & seed SQLite database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    await DbInitializer.InitializeAsync(db);
}

// 7. Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("POS Local Edge API")
               .WithTheme(ScalarTheme.Moon)
               .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch);
    });
}

app.UseCors("PosFrontendPolicy");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<PosHub>("/hubs/pos");

app.Run();

