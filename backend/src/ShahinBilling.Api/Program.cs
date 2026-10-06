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

// ---------- Rate limiting: slows down password and code guessing ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Lets the automated tests start the whole API in memory.</summary>
public partial class Program { }
