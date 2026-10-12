using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RondiTrack.Authorization;
using RondiTrack.Exceptions;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Health;
using RondiTrack.Infrastructure;
using RondiTrack.Services;
using RondiTrack.Validation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// 1. Controllers and OpenAPI
// --------------------------------------------------

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<CorrelationProblemDetailsFilter>();
});

builder.Services.AddOpenApi();

// --------------------------------------------------
// 2. OpenTelemetry
// --------------------------------------------------

const string serviceName = "ronditrack-api";

var otlpEndpoint =
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

var enableOtlpExport =
    !string.IsNullOrWhiteSpace(otlpEndpoint);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService(serviceName);
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments(
                        "/health");
            })
            .AddSource("Npgsql")
            .AddSource(PayoutService.ActivitySourceName);

        if (enableOtlpExport)
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();

        metrics.AddMeter(
            RondiTrack.Telemetry.RondiTrackMetrics.MeterName);

        if (enableOtlpExport)
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogLevel.Warning);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = false;
    options.IncludeScopes = true;

    if (enableOtlpExport)
    {
        options.AddOtlpExporter();
    }
});

// --------------------------------------------------
// 3. JWT authentication
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
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKeyBytes),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                var correlationId =
                    Activity.Current?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;

                var problem = new ProblemDetails
                {
                    Status = 401,
                    Title = "Authentication required",
                    Detail =
                        "Valid authentication credentials are required."
                };

                problem.Extensions["correlationId"] =
                    correlationId;

                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json");
            },

            OnForbidden = async context =>
            {
                var correlationId =
                    Activity.Current?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;

                var problem = new ProblemDetails
                {
                    Status = 403,
                    Title = "Forbidden",
                    Detail =
                        "You are not authorized to perform this operation."
                };

                problem.Extensions["correlationId"] =
                    correlationId;

                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json");
            }
        };
    });

// --------------------------------------------------
// 4. Database
// --------------------------------------------------

builder.Services.AddDbContext<RondiTrackDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("RondiTrack"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

// --------------------------------------------------
// 5. Stores and services
// --------------------------------------------------

builder.Services.AddScoped<
    IStokvelStore,
    EfStokvelStore>();

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
// 6. Exception handling
// --------------------------------------------------

builder.Services.AddExceptionHandler<
    RondiTrack.Exceptions.RondiTrackExceptionHandler>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] =
            Activity.Current?.TraceId.ToString()
            ?? context.HttpContext.TraceIdentifier;
    };
});

// --------------------------------------------------
// 7. Health checks
// --------------------------------------------------

// Liveness checks whether the API is running.
// Readiness checks PostgreSQL without EF retry delays.
//
// The five-second health-check timeout provides
// a deadline for the readiness check.
builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadinessHealthCheck>(
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" },
        timeout: TimeSpan.FromSeconds(5));

// --------------------------------------------------
// 8. FluentValidation
// --------------------------------------------------

foreach (var validator in typeof(Program).Assembly.GetTypes()
    .Where(type => !type.IsAbstract && !type.IsInterface)
    .SelectMany(type => type.GetInterfaces()
        .Where(service =>
            service.IsGenericType &&
            service.GetGenericTypeDefinition() ==
                typeof(IValidator<>))
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
// 9. Authorization
// --------------------------------------------------

builder.Services.AddScoped<
    IAuthorizationHandler,
    StokvelRoleHandler>();

builder.Services.AddScoped<
    IStokvelAccessService,
    StokvelAccessService>();

builder.Services.AddAuthorization(options =>
{
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
// 10. Build application
// --------------------------------------------------

var app = builder.Build();

// --------------------------------------------------
// 11. Middleware
// --------------------------------------------------

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------
// 12. Health endpoints
// --------------------------------------------------

var healthOptions = new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        // Only return a safe, plain-text status.
        context.Response.ContentType = "text/plain";

        var result = report.Status == HealthStatus.Healthy
            ? "Healthy"
            : "Unhealthy";

        await context.Response.WriteAsync(result);
    }
};

// Liveness does not depend on PostgreSQL.
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = healthOptions.ResponseWriter
    })
    .AllowAnonymous();

// Readiness checks PostgreSQL with a timeout.
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check =>
            check.Tags.Contains("ready"),
        ResponseWriter = healthOptions.ResponseWriter
    })
    .AllowAnonymous();

// --------------------------------------------------
// 13. API controllers
// --------------------------------------------------

app.MapControllers();

app.Run();

// Required by integration tests.
public partial class Program
{
}
