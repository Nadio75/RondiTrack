using System.Globalization;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
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
using RondiTrack.Common;
using RondiTrack.Exceptions;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Health;
using RondiTrack.Infrastructure;
using RondiTrack.Services;
using RondiTrack.Validation;
using Scalar.AspNetCore;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// 1. Controllers and OpenAPI
// --------------------------------------------------

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.Filters.Add<CorrelationProblemDetailsFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling =
            JsonNumberHandling.Strict;
    });
    builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = new ModelStateDictionary();

        foreach (var entry in context.ModelState)
        {
            var key = entry.Key;

            if (!string.IsNullOrWhiteSpace(key))
            {
                var dot = key.LastIndexOf('.');

                var prefix = dot >= 0
                    ? key[..(dot + 1)]
                    : string.Empty;

                var property = dot >= 0
                    ? key[(dot + 1)..]
                    : key;

                key = prefix +
                    JsonNamingPolicy.CamelCase.ConvertName(property);
            }

            foreach (var error in entry.Value?.Errors
                         ?? Enumerable.Empty<ModelError>())
            {
                errors.AddModelError(
                    key,
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage);
            }
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    };
});

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        if (context.JsonTypeInfo.Type == typeof(decimal) ||
            context.JsonTypeInfo.Type == typeof(decimal?))
        {
            schema.Type = Microsoft.OpenApi.JsonSchemaType.Number;
            schema.Format = "double";
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    });
});
// Only configured frontend origins may make credentialed browser requests.
// Development uses http://localhost:3000; production defaults to none.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Frontend, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "traceparent")
            .WithExposedHeaders("Retry-After")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// --------------------------------------------------
// Rate limiting: configurable fixed windows.
// Login is partitioned by IP; financial operations by authenticated user.
// --------------------------------------------------
var loginLimit = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
var loginWindowSeconds = builder.Configuration.GetValue("RateLimiting:Login:WindowSeconds", 60);
var financialLimit = builder.Configuration.GetValue("RateLimiting:Financial:PermitLimit", 30);
var financialWindowSeconds = builder.Configuration.GetValue("RateLimiting:Financial:WindowSeconds", 60);

if (loginLimit <= 0 || loginWindowSeconds <= 0 ||
    financialLimit <= 0 || financialWindowSeconds <= 0)
{
    throw new InvalidOperationException("RateLimiting limits and windows must be positive integers.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Login, context =>
    {
        // Prefer the actual remote IP. Only trust forwarded headers when
        // configured for a known reverse proxy; never trust a raw X-Forwarded-For.
        var ip = context.Connection.RemoteIpAddress?.MapToIPv6().ToString()
                 ?? "unknown-ip";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"login:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginLimit,
                Window = TimeSpan.FromSeconds(loginWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy(RateLimitPolicies.Financial, context =>
    {
        // A financial request must have a validated identity.
        // The fallback is deliberately shared, never derived from an untrusted header.
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub")
                     ?? "unauthenticated";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"financial:{userId}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = financialLimit,
                Window = TimeSpan.FromSeconds(financialWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (rejection, cancellationToken) =>
    {
        var response = rejection.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        var wait = rejection.Lease.TryGetMetadata(
            MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? retryAfter
            : TimeSpan.FromSeconds(1);

        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Request limit reached. Wait before trying again."
        };
        problem.Extensions["correlationId"] =
            Activity.Current?.TraceId.ToString()
            ?? rejection.HttpContext.TraceIdentifier;

        await response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };
});

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
builder.Services.AddSingleton(TimeProvider.System);
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

app.UseRouting();
app.UseCors(CorsPolicies.Frontend);

app.UseAuthentication();
app.UseRateLimiter();
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
