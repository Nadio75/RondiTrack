
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

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// 1. Controller and OpenAPI registration
// --------------------------------------------------

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddOpenApi();

// --------------------------------------------------
// 2. JWT authentication configuration
// --------------------------------------------------

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

        // Return standardized ProblemDetails for 401 and 403.
        // The content type must be set BEFORE the response starts.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = 401,
                        Title = "Authentication required",
                        Detail =
                            "Valid authentication credentials are required."
                    },
                    options: null,
                    contentType: "application/problem+json");
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = 403,
                        Title = "Forbidden",
                        Detail =
                            "You are not authorized to perform this operation."
                    },
                    options: null,
                    contentType: "application/problem+json");
            }
        };
    });

// --------------------------------------------------
// 3. Database configuration
// --------------------------------------------------

builder.Services.AddDbContext<RondiTrackDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("RondiTrack"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// --------------------------------------------------
// 4. Data stores and application services
// --------------------------------------------------

builder.Services.AddScoped<IStokvelStore, EfStokvelStore>();

builder.Services.AddSingleton<
    IContributionStore,
    InMemoryContributionStore>();

builder.Services.AddSingleton<
    IIdempotencyStore,
    InMemoryIdempotencyStore>();

builder.Services.AddScoped<
    IContributionCycleStore,
    EfContributionCycleStore>();

builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<StokvelMembershipService>();
builder.Services.AddScoped<RecordContributionService>();

builder.Services.AddScoped<PasswordHasher<User>>();

// --------------------------------------------------
// 5. Exception handling and ProblemDetails
// --------------------------------------------------

builder.Services.AddExceptionHandler<
    RondiTrack.Exceptions.RondiTrackExceptionHandler>();

builder.Services.AddProblemDetails();

// --------------------------------------------------
// 6. FluentValidation registration
// --------------------------------------------------

foreach (var validator in typeof(Program).Assembly.GetTypes()
    .Where(type => !type.IsAbstract && !type.IsInterface)
    .SelectMany(type => type.GetInterfaces()
        .Where(service =>
            service.IsGenericType &&
            service.GetGenericTypeDefinition() == typeof(IValidator<>))
        .Select(service => new
        {
            service,
            implementation = type
        })))
{
    builder.Services.AddTransient(
        validator.service,
        validator.implementation);
}

// --------------------------------------------------
// 7. Stokvel authorization services and policies
// --------------------------------------------------

builder.Services.AddScoped<
    IAuthorizationHandler,
    StokvelRoleHandler>();

builder.Services.AddScoped<
    IStokvelAccessService,
    StokvelAccessService>();

builder.Services.AddAuthorization(options =>
{
    // All endpoints require authentication by default,
    // except those explicitly marked AllowAnonymous.
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

    options.AddPolicy("StokvelMember", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.AddRequirements(
            new StokvelRoleRequirement("Member"));
    });

    options.AddPolicy(
        "StokvelTreasurerOrChairperson",
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.AddRequirements(
                new StokvelRoleRequirement(
                    "TreasurerOrChairperson"));
        });

    options.AddPolicy("StokvelChairperson", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.AddRequirements(
            new StokvelRoleRequirement("Chairperson"));
    });
});

// --------------------------------------------------
// 8. Build application
// --------------------------------------------------

var app = builder.Build();

// --------------------------------------------------
// 9. Middleware pipeline
// --------------------------------------------------

// Handles application exceptions consistently.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

// Authentication must run before authorization.
app.UseAuthentication();
app.UseAuthorization();

// Register controller endpoints.
app.MapControllers();

app.Run();

// Allows WebApplicationFactory<Program>
// to start the application in integration tests.
public partial class Program
{
}
