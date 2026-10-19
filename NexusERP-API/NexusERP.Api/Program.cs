using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.IdentityModel.Tokens;
using NexusERP.Api.Routing;
using NexusERP.Application;
using NexusERP.Application.Common;
using NexusERP.Infrastructure;
using NexusERP.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment.IsDevelopment());

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException($"'{JwtSettings.SectionName}' section is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as issued ("sub", "role", "companyId") instead of mapping to long URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            NameClaimType = AppClaimTypes.UserId,
            RoleClaimType = AppClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Default 401/403 have empty bodies; keep them in the { "ok": false, "error": "..." } shape.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Giriş tələb olunur."));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Bu əməliyyat üçün icazəniz yoxdur."));
            }
        };
    });

builder.Services.AddAuthorization();

// The React frontend runs on its own origin (Vite dev server); list allowed origins in "Cors:AllowedOrigins".
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services
    .AddControllers(options =>
        options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer())))
    // Enums go over the wire as camelCase names ("draft", "approved") rather than numbers.
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Missing or malformed JSON bodies: return our error shape instead of ProblemDetails.
        options.InvalidModelStateResponseFactory = _ =>
            new BadRequestObjectResult(new ErrorResponse("Sorğu formatı yanlışdır."));
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Operation ids are "<Controller>_<Action>" without the "Async" suffix (Products_GetAll, Sales_Create, ...).
    // The controller prefix keeps them unique, since many controllers share action names like GetAll and Create.
    options.CustomOperationIds(api =>
        api.ActionDescriptor is ControllerActionDescriptor action
            ? $"{action.ControllerName}_{TrimAsyncSuffix(action.ActionName)}"
            : null);

    // api/company is a legacy alias of api/companies kept for existing clients; documenting both
    // would emit every company operation twice with the same operation id.
    options.DocInclusionPredicate((_, api) =>
        api.RelativePath is not { } path
        || !(path.Equals("api/company", StringComparison.OrdinalIgnoreCase)
             || path.StartsWith("api/company/", StringComparison.OrdinalIgnoreCase)));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Before HTTPS redirection and auth, so preflight (OPTIONS) requests are answered rather than redirected or rejected.
app.UseCors(FrontendCorsPolicy);

// In Development the frontend calls the plain-HTTP endpoint; a 307 to HTTPS would turn every cross-origin call into a redirect.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string TrimAsyncSuffix(string name) =>
    name.EndsWith("Async", StringComparison.Ordinal) ? name[..^"Async".Length] : name;
