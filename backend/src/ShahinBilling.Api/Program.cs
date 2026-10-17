using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Pdf;
using ShahinBilling.Api.Reports;
using ShahinBilling.Api.Services;
using ShahinBilling.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;   // free for businesses under USD 1M annual revenue
MongoConfig.Register();

// ---------- Settings ----------
builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be set and at least 32 characters. Copy appsettings.example.json to appsettings.Development.json and fill it in.");

// ---------- Services ----------
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SessionValidator>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddSingleton<CaptchaService>();
builder.Services.AddSingleton<BackupExporter>();
builder.Services.AddHostedService<BackupHostedService>();
builder.Services.AddHostedService<ReminderHostedService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<ProductTypeService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PortalAccessService>();
builder.Services.AddScoped<InvoiceNumberService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddScoped<BillFormatService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<InvoicePdfService>();
builder.Services.AddValidatorsFromAssemblyContaining<ItemValidator>();

builder.Services
    .AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// ---------- Auth ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.Events = new JwtBearerEvents
        {
            // A valid token is not enough: the user must still be switched on and not have ended their sessions
            OnTokenValidated = async ctx =>
            {
                var sessions = ctx.HttpContext.RequestServices.GetRequiredService<SessionValidator>();
                if (!await sessions.IsValidAsync(ctx.Principal!)) ctx.Fail("This session has ended.");
            }
        };
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// ---------- Rate limiting ----------
// 1) Every /api call counts against a limit per logged-in user (or per address for people who are not logged in), so a runaway
//    browser tab, a script or an attacker cannot flood the API.  2) Login, the login picture and the heavy file-making endpoints
//    (PDF, Excel, backup, import) also have their own, tighter limits.  Limits are per minute and live in the "RateLimit" settings.
// Behind the Cloudflare Tunnel every visitor reaches the API from the same local address, so ClientIp reads the real address.
var rateLimitOn = builder.Configuration.GetValue("RateLimit:Enabled", true);
int Limit(string name, int fallback) => builder.Configuration.GetValue($"RateLimit:{name}", fallback);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (context, token) =>
    {
        var res = context.HttpContext.Response;
        // how long to wait: the limiter's own answer when it gives one, otherwise the length of the window (one minute)
        var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
        res.Headers.RetryAfter = wait.ToString(System.Globalization.CultureInfo.InvariantCulture);
        res.ContentType = "application/problem+json";
        await res.WriteAsync("{\"title\":\"Too many requests. Please wait a minute and try again.\",\"status\":429}", token);
    };

    // who is asking: the logged-in user, or else the visitor's address
    static string Who(HttpContext ctx) => ctx.User.Identity?.IsAuthenticated == true
        ? "u:" + (ctx.User.FindFirst(ShahinBilling.Api.Auth.ClaimsExtensions.UserIdClaim)?.Value ?? ctx.User.Identity.Name)
        : "ip:" + ClientIp.Of(ctx);

    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (!rateLimitOn || !ctx.Request.Path.StartsWithSegments("/api")) return RateLimitPartition.GetNoLimiter("none");
        var signedIn = ctx.User.Identity?.IsAuthenticated == true;
        return RateLimitPartition.GetSlidingWindowLimiter("all:" + Who(ctx), _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = signedIn ? Limit("GlobalPerMinute", 300) : Limit("AnonymousPerMinute", 60),
            Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
        });
    });

    // login, sign-up and the email-code endpoints: slows down password and code guessing
    o.AddPolicy("auth", ctx => !rateLimitOn ? RateLimitPartition.GetNoLimiter("none") : RateLimitPartition.GetFixedWindowLimiter(
        "auth:" + ClientIp.Of(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // asking for a new login picture: more room than the login itself, since people press refresh, but still limited
    o.AddPolicy("captcha", ctx => !rateLimitOn ? RateLimitPartition.GetNoLimiter("none") : RateLimitPartition.GetFixedWindowLimiter(
        "captcha:" + ClientIp.Of(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("CaptchaPerMinute", 40), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // endpoints that take real work on the server (PDFs, Excel files, backup, import)
    o.AddPolicy("heavy", ctx => !rateLimitOn ? RateLimitPartition.GetNoLimiter("none") : RateLimitPartition.GetFixedWindowLimiter(
        "heavy:" + Who(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("HeavyPerMinute", 30), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- CORS ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Shahin Billing API", Version = "v1" });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await app.Services.GetRequiredService<MongoContext>().EnsureIndexesAsync();

app.UseMiddleware<SecurityHeadersMiddleware>();   // first, so every answer carries the headers, even errors and blocked requests
app.UseMiddleware<HttpsOnlyMiddleware>();       // Security:RequireHttps: stops plain-http requests that got past Cloudflare
app.UseMiddleware<ErrorHandlingMiddleware>();

// An unknown API address, a wrong method and so on come back with no body by default. Give them a small JSON answer like every other error.
app.UseStatusCodePages(async ctx =>
{
    var http = ctx.HttpContext;
    var res = http.Response;
    if (!http.Request.Path.StartsWithSegments("/api") || res.HasStarted || res.ContentLength > 0 || !string.IsNullOrEmpty(res.ContentType)) return;
    var title = res.StatusCode switch
    {
        404 => "That address does not exist.",
        405 => "That action is not allowed here.",
        401 => "Please log in.",
        403 => "You do not have access to that.",
        _ => "Something went wrong.",
    };
    res.ContentType = "application/problem+json";
    await res.WriteAsync($"{{\"title\":\"{title}\",\"status\":{res.StatusCode}}}");
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();   // after authentication, so the limit can be per user
app.UseAuthorization();
app.MapControllers();

// For the website's "server is down" screen, uptime monitors and Cloudflare health checks: 200 when the API and the database both answer.
app.MapGet("/api/health", async (MongoContext mongo) =>
{
    try
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await mongo.Db.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: limit.Token);
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.Json(new { status = "database unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.Run();

/// <summary>Lets the automated tests start the whole API in memory.</summary>
public partial class Program { }
