using Scalar.AspNetCore;
using RondiTrack.Data;
var builder = WebApplication.CreateBuilder(args);

// --- Services registration (before Build()) ---

// Registers Controllers support — required since we're using ControllerBase classes.
builder.Services.AddControllers();

// Registers the native .NET OpenAPI document generator (built into .NET 9/10,
// this is what produces the JSON spec describing your API's shape).
builder.Services.AddOpenApi();

// Our in-memory data store, shared as a single instance for the app's lifetime.
builder.Services.AddSingleton<IStokvelStore, InMemoryStokvelStore>();

var app = builder.Build();

// --- Middleware pipeline (after Build(), before Run()) ---

if (app.Environment.IsDevelopment())
{
    // This exposes the raw OpenAPI JSON document at /openapi/v1.json
    app.MapOpenApi();

    // This maps the actual Scalar UI, which reads that JSON document and
    // renders the interactive browser page. By default it's served at /scalar
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Wires up all your [ApiController] classes (UsersController, StokvelsController)
// to their routes.
app.MapControllers();

app.Run();