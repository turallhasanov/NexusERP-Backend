using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.IdentityModel.Tokens;
using NexusERP.Api.Logging;
using NexusERP.Api.Middlewares;
using NexusERP.Api.Routing;
using NexusERP.Application;
using NexusERP.Application.Common;
using NexusERP.Application.SystemInfo;
using NexusERP.Infrastructure;
using NexusERP.Infrastructure.Authentication;
using NexusERP.Infrastructure.Persistence;
using Serilog;

// Until the configuration is read, a startup failure is still written to the console.
Log.Logger = SerilogSetup.CreateBootstrapLogger();

// WebApplicationFactory (integration tests) and dotnet-ef stop this entry point with HostAbortedException once the
// host is built; that host keeps running and writes through the logger, so the logger must not be closed then.
var hostAborted = false;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Every ILogger<T> (services, GlobalExceptionHandler, ...) writes through Serilog: the console and
    // logs/nexus-<yyyyMMdd>.txt, a new file each day, 30 kept (the "Serilog" section of appsettings.json).
    Log.Logger = SerilogSetup.CreateLogger(builder.Configuration, builder.Environment);
    builder.Services.AddSerilog(Log.Logger);

    // Add services to the container.
    builder.Services.AddApplicationServices();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<NexusERP.Application.Common.Interfaces.ICurrentUserContext, NexusERP.Api.Services.HttpCurrentUserContext>();

    // Live support chat: same JSON shape as the REST API (camelCase, enums as names).
    builder.Services
        .AddSignalR()
        .AddJsonProtocol(options =>
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
    builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, NexusERP.Api.Hubs.SubClaimUserIdProvider>();
    builder.Services.AddScoped<NexusERP.Application.Support.ISupportNotifier, NexusERP.Api.Hubs.SignalRSupportNotifier>();
    builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment.IsDevelopment());
    builder.Services.AddOptions<SystemInfoSettings>().Bind(builder.Configuration.GetSection(SystemInfoSettings.SectionName));

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
                // Browsers cannot set headers on WebSockets: the hub's token comes as ?access_token=, on /hubs only.
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];

                    if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        context.Token = token;

                    return Task.CompletedTask;
                },
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

    // The React frontend runs on its own origin (Vite dev server); list allowed origins in "Cors:AllowedOrigins"
    // (appsettings.Development.json covers localhost and 127.0.0.1, and 5174, where Vite moves when 5173 is taken).
    // A request from any other origin gets no CORS headers; the browser then hides the response and the frontend
    // reports "Serverə qoşulmaq mümkün olmadı", the same as when the API is down.
    const string FrontendCorsPolicy = "Frontend";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
        options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Only valid with explicit origins (never AllowAnyOrigin); lets the SignalR negotiate request use credentials.
            .AllowCredentials()));

    // Product photos: one instance is both the writer the controller uses and the reader the application layer uses.
    builder.Services.AddSingleton<NexusERP.Api.Storage.ProductImageStore>();
    builder.Services.AddSingleton<NexusERP.Application.Products.IProductImageFiles>(
        services => services.GetRequiredService<NexusERP.Api.Storage.ProductImageStore>());

    builder.Services
        .AddControllers(options =>
        {
            options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
            // Platform administrators viewing a company (impersonation tokens) may read but never write.
            options.Filters.Add<ImpersonationReadOnlyFilter>();
        })
        // Enums go over the wire as camelCase names ("draft", "approved") rather than numbers.
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
        .ConfigureApiBehaviorOptions(options =>
        {
            // Missing or malformed JSON bodies: return our error shape instead of ProblemDetails.
            options.InvalidModelStateResponseFactory = _ =>
                new BadRequestObjectResult(new ErrorResponse("Sorğu formatı yanlışdır."));
        });
    // Unhandled exceptions become a logged ProblemDetails 500 (see GlobalExceptionHandler). Only exceptions are affected:
    // Result failures keep { ok: false, error }, and body-less 401/403/404 responses stay as they are (no UseStatusCodePages).
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<NexusERP.Api.ExceptionHandling.GlobalExceptionHandler>();

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

        // The /// <summary> of each action is its summary in Swagger UI (see GenerateDocumentationFile in the csproj).
        var xmlDocs = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        if (File.Exists(xmlDocs))
            options.IncludeXmlComments(xmlDocs);

        // api/company is a legacy alias of api/companies kept for existing clients; documenting both
        // would emit every company operation twice with the same operation id.
        options.DocInclusionPredicate((_, api) =>
            api.RelativePath is not { } path
            || !(path.Equals("api/company", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("api/company/", StringComparison.OrdinalIgnoreCase)));
    });

    var app = builder.Build();

    app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

    // The seeder below queries Users, so the schema must match the model first (Development applies pending migrations).
    await app.Services.MigrateDatabaseAsync(applyPending: app.Environment.IsDevelopment());

    // Creates the platform administrator from the "SuperAdmin" section (if configured) before serving requests.
    await app.Services.SeedSuperAdminAsync();

    // Configure the HTTP request pipeline.

    // One line per request (method, path, status, duration) in the log. Ahead of the exception handler, so it records the
    // status the handler set (500) instead of logging the exception a second time; it never changes a response.
    app.UseSerilogRequestLogging();

    // Catches exceptions from everything below (CORS, auth, controllers). CORS headers still reach the 500, since the
    // CORS middleware adds them when the response starts, so the browser can read the error body.
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        // Shows each operation id (Products_GetAll, Sales_Delete, ...) next to its route.
        app.UseSwaggerUI(options => options.DisplayOperationId());
    }

    // Before HTTPS redirection and auth, so preflight (OPTIONS) requests are answered rather than redirected or rejected.
    app.UseCors(FrontendCorsPolicy);

    // In Development the frontend calls the plain-HTTP endpoint; a 307 to HTTPS would turn every cross-origin call into a redirect.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Product photos at /uploads/products/{file}. File names are unique and never change, so browsers may cache them.
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            app.Services.GetRequiredService<NexusERP.Api.Storage.ProductImageStore>().UploadsRoot),
        RequestPath = NexusERP.Api.Storage.ProductImageStore.RequestPath,
        OnPrepareResponse = context =>
            context.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable",
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NexusERP.Api.Hubs.SupportChatHub>(NexusERP.Api.Hubs.SupportChatHub.Path);

    app.Run();
}
catch (HostAbortedException)
{
    hostAborted = true;
    throw;
}
catch (Exception exception)
{
    Log.Fatal(exception, "NexusERP API stopped: it failed to start or crashed while running.");
    Environment.ExitCode = 1;
}
finally
{
    if (!hostAborted)
        Log.CloseAndFlush();
}

static string TrimAsyncSuffix(string name) =>
    name.EndsWith("Async", StringComparison.Ordinal) ? name[..^"Async".Length] : name;
