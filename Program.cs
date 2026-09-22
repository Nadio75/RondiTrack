using Scalar.AspNetCore;    
using RondiTrack.Data;
using RondiTrack.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Services registration (before Build()) ---

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Our in-memory data stores, each shared as a single instance for the app's lifetime.
builder.Services.AddSingleton<IStokvelStore, InMemoryStokvelStore>();
builder.Services.AddSingleton<IContributionStore, InMemoryContributionStore>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Services hold no state of their own — they just orchestrate the stores above,
// which are already singletons — so these are registered as Scoped.
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<RecordContributionService>();

builder.Services.AddProblemDetails();

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