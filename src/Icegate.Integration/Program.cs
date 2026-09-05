using System.Reflection;
using Icegate.Integration.Configuration;
using Icegate.Integration.Data;
using Icegate.Integration.Http;
using Icegate.Integration.Middleware;
using Icegate.Integration.Services;
using Icegate.Integration.Services.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ---- Environment overlay: appsettings.{Icegate:Environment}.json (UAT / PROD) ----
    // ASPNETCORE_ENVIRONMENT already loads appsettings.{ASPNETCORE_ENVIRONMENT}.json.
    // We additionally honor an explicit Icegate:Environment switch (UAT/PROD) so the ICEGATE
    // environment can be changed independently of the hosting environment name.
    var icegateEnvironment = builder.Configuration["Icegate:Environment"] ?? "UAT";
    builder.Configuration.AddJsonFile($"appsettings.{icegateEnvironment}.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();

    // ---- Logging (Serilog, structured, with CorrelationId enrichment) ----
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Icegate.Integration")
        .Enrich.WithProperty("IcegateEnvironment", icegateEnvironment)
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(AppContext.BaseDirectory, "Logs", "icegate-integration-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

    // ---- Configuration binding ----
    builder.Services.Configure<IcegateSettings>(builder.Configuration.GetSection(IcegateSettings.SectionName));
    builder.Services.Configure<InternalApiSettings>(builder.Configuration.GetSection(InternalApiSettings.SectionName));
    builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection(FileStorageSettings.SectionName));
    builder.Services.Configure<List<IcegateClientSettings>>(builder.Configuration.GetSection(IcegateClientSettings.SectionName));

    // ---- Database (transaction log) ----
    var connectionString = builder.Configuration.GetConnectionString("IcegateDb");
    builder.Services.AddDbContext<IcegateDbContext>(options =>
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseSqlite("Data Source=icegate.db");
        }
        else if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) &&
                 connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connectionString);
        }
        else
        {
            options.UseSqlServer(connectionString);
        }
    });

    // ---- Outbound HTTP to ICEGATE: IHttpClientFactory + centralized timeout + Polly retry ----
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Icegate:TimeoutSeconds") ?? 120;
    var retryCount = builder.Configuration.GetValue<int?>("Icegate:NetworkRetryCount") ?? 2;

    builder.Services.AddHttpClient(IcegateHttpClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy(retryCount));

    // ---- Application services ----
    // IIcegateHttpClient, IIcegateAuthenticationService and IIcegateTokenService are singletons:
    // they hold no per-request/scoped state (IcegateTokenService's in-memory token cache is
    // deliberately process-wide so the token is generated once and reused, not per request).
    builder.Services.AddSingleton<IIcegateHttpClient, IcegateHttpClient>();
    builder.Services.AddSingleton<IIcegateClientRegistry, IcegateClientRegistry>();
    builder.Services.AddSingleton<IIcegateAuthenticationService, IcegateAuthenticationService>();
    builder.Services.AddSingleton<IIcegateTokenService, IcegateTokenService>();
    builder.Services.AddScoped<ITokenRetryExecutor, TokenRetryExecutor>();
    builder.Services.AddScoped<IIcegateFileSubmissionService, IcegateFileSubmissionService>();
    builder.Services.AddScoped<IIcegateAcknowledgementService, IcegateAcknowledgementService>();
    builder.Services.AddScoped<IIcegateZipAcknowledgementService, IcegateZipAcknowledgementService>();
    builder.Services.AddScoped<ITransactionLogService, TransactionLogService>();

    // ---- MVC / Swagger ----
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.Configure<FormOptions>(options =>
    {
        var maxSize = builder.Configuration.GetValue<long?>("FileStorage:MaxInboundFileSizeBytes") ?? 10 * 1024 * 1024;
        options.MultipartBodyLengthLimit = maxSize + 1024 * 1024; // small overhead for multipart framing
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ICEGATE SCMTR Integration API",
            Version = "v1",
            Description = "Middleware .NET 8 Web API between the legacy .NET Framework 4.0 application and ICEGATE " +
                          "(SCMTR Open API Filing - API Contract Document v1.6). Handles ICEGATE authentication, " +
                          "token lifecycle, multipart submission/acknowledgement, and transaction logging."
        });

        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Name = "X-API-KEY",
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "Internal API key used to authenticate the legacy application against THIS API. Never the ICEGATE API key."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
                Array.Empty<string>()
            }
        });

        var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlFile))
        {
            options.IncludeXmlComments(xmlFile);
        }
    });

    var app = builder.Build();

    // ---- Ensure the transaction log database exists (dev convenience; use migrations in real deployments) ----
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IcegateDbContext>();
        dbContext.Database.EnsureCreated();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseMiddleware<ApiKeyAuthMiddleware>();

    app.MapControllers();

    Log.Information("Icegate.Integration starting. IcegateEnvironment={IcegateEnvironment}", icegateEnvironment);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Icegate.Integration terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount) =>
    HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx and 408 - never business validation 4xx
        .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

/// <summary>Marker for WebApplicationFactory-based integration tests.</summary>
public partial class Program
{
}
