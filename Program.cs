using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using Scalar.AspNetCore;    
using FluentValidation;
using RondiTrack.Data;
using RondiTrack.Services;
using RondiTrack.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Authorization;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

// --- Services registration (before Build()) ---

builder.Services.AddControllers();
builder.Services.AddOpenApi();
// JWT authentication: validate signature, issuer, audience and lifetime.
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Missing required configuration: Jwt:Issuer.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Missing required configuration: Jwt:Audience.");

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Missing required configuration: Jwt:SigningKey.");

byte[] jwtKeyBytes;

try
{
    jwtKeyBytes = Convert.FromBase64String(jwtSigningKey);
}
catch (FormatException exception)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be valid Base64.",
        exception);
}

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must decode to at least 32 bytes.");
}

builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
{
    OnChallenge = async context =>
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 401,
            Title = "Authentication required",
            Detail = "Valid authentication credentials are required."
        });
    },

    OnForbidden = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 403,
            Title = "Forbidden",
            Detail = "You are not authorized to perform this operation."
        });
    }
};
        ;
    });

// Our in-memory data stores, each shared as a single instance for the app's lifetime.
builder.Services.AddScoped<IStokvelStore, EfStokvelStore>();
builder.Services.AddSingleton<IContributionStore, InMemoryContributionStore>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<IContributionCycleStore, EfContributionCycleStore>();builder.Services.AddDbContext<RondiTrack.Infrastructure.RondiTrackDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("RondiTrack"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

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
builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddScoped<IAuthorizationHandler, StokvelRoleHandler>();
builder.Services.AddScoped<IStokvelAccessService, StokvelAccessService>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
      options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("StokvelMember", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new StokvelRoleRequirement("Member"));
    });

    options.AddPolicy("StokvelTreasurerOrChairperson", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(
            new StokvelRoleRequirement("TreasurerOrChairperson"));
    });
    options.AddPolicy("StokvelChairperson", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.AddRequirements(new StokvelRoleRequirement("Chairperson"));
});
});


//builder.Services.AddProblemDetails();

var app = builder.Build();
// Temporary manual volume-seeding command.
// Run with: dotnet run -- --seed-volume

// --- Middleware pipeline (after Build(), before Run()) ---

// Must come before anything that could throw — this is what actually invokes
// RondiTrackExceptionHandler whenever an action, filter, or piece of middleware throws.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // This exposes the raw OpenAPI JSON document at /openapi/v1.json
    app.MapOpenApi().AllowAnonymous();

    // This maps the actual Scalar UI, which reads that JSON document and
    // renders the interactive browser page. By default it's served at /scalar
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Wires up all your [ApiController] classes (UsersController, StokvelsController)
// to their routes.
app.MapControllers();

app.Run();
// Makes the implicit top-level Program class visible to the test project,
// so WebApplicationFactory<Program> can spin the whole app up for tests.
public partial class Program { }

