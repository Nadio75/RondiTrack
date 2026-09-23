using Scalar.AspNetCore;    
using FluentValidation;
using RondiTrack.Data;
using RondiTrack.Services;
using RondiTrack.Validation;

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
// Registers our handler; if it's ever NOT the one that ends up formatting a response
// (shouldn't happen, since it always returns true), AddProblemDetails() is the fallback
// that still guarantees a problem+json shape.
builder.Services.AddExceptionHandler<RondiTrack.Exceptions.RondiTrackExceptionHandler>();
builder.Services.AddProblemDetails();

// Auto-registers every AbstractValidator<T> in this project — no need to list them by hand.
// Register validators from this assembly without requiring the FluentValidation
// dependency-injection extension package.
foreach (var validator in typeof(Program).Assembly.GetTypes()
    .Where(type => !type.IsAbstract && !type.IsInterface)
    .SelectMany(type => type.GetInterfaces()
        .Where(service => service.IsGenericType &&
            service.GetGenericTypeDefinition() == typeof(IValidator<>))
        .Select(service => new { service, implementation = type })))
{
    builder.Services.AddTransient(validator.service, validator.implementation);
}

// Runs ValidationFilter on every controller action, before the action body executes.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

//builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Middleware pipeline (after Build(), before Run()) ---

// Must come before anything that could throw — this is what actually invokes
// RondiTrackExceptionHandler whenever an action, filter, or piece of middleware throws.
app.UseExceptionHandler();

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