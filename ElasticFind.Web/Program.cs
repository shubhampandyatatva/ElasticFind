using System.Security.Claims;
using System.Text;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.Implementations;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Implementations;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rotativa.AspNetCore;
using Nest;
using ElasticFind.Web.MiddleWare;
using ElasticFind.Web.SerilogConfig;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ElasticFindContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

SerilogConfiguration.ConfigureSerilog(builder.Configuration);
builder.Services.AddSingleton(Log.Logger);
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IResetPasswordService, ResetPasswordService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IUploadImageService, UploadImageService>();
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddScoped<IPreviewFileService, PreviewFileService>();
builder.Services.AddScoped<IExportService, ExportService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue;
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
   .AddJwtBearer(options =>
   {
       options.RequireHttpsMetadata = false;
       options.SaveToken = true;
       options.TokenValidationParameters = new TokenValidationParameters
       {
           ValidateIssuer = true,
           ValidateAudience = true,
           ValidateLifetime = true,
           ValidateIssuerSigningKey = true,
           ValidIssuer = builder.Configuration["Jwt:Issuer"],
           ValidAudience = builder.Configuration["Jwt:Audience"],
           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
           RoleClaimType = ClaimTypes.Role
       };

       options.Events = new JwtBearerEvents
       {
           OnMessageReceived = context =>
           {
               string? token = context.Request.Cookies["JwtToken"];
               if (!string.IsNullOrEmpty(token))
               {
                   context.Request.Headers["Authorization"] = "Bearer " + token;
               }
               return Task.CompletedTask;
           },
           OnChallenge = context =>
           {
               if (!context.Response.HasStarted)
               {
                   context.Response.Redirect("/StatusCode/401");
               }
               context.HandleResponse();
               return Task.CompletedTask;
           },
           OnForbidden = context =>
           {
               context.Response.Redirect("/StatusCode/403");
               return Task.CompletedTask;
           }
       };
   });

var pool = new SingleNodeConnectionPool(new Uri("https://localhost:9200"));

var settings = new ConnectionSettings(pool)
    .ServerCertificateValidationCallback((sender, cert, chain, errors) => true) // Ignore cert errors
    .BasicAuthentication("elastic", "158xkDDd9Qn1fajXw0K1")
    // .BasicAuthentication("elastic", "xU0dIO7RHrWFwVl-cgb*")
    .DisableDirectStreaming()
    .EnableDebugMode()
    .DefaultIndex(builder.Configuration["Elasticsearch:IndexName"] ?? "documents");

var client = new ElasticClient(settings);

// Optional: Register for DI so you can inject IElasticClient later
builder.Services.AddSingleton<IElasticClient>(client);

try
{
    await ValidateAndInitializeElasticsearchAsync(client);
}
catch (Exception ex)
{
    StartupDiagnostics.ElasticsearchError = ex.Message;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("OnlyOfficePolicy", builder =>
    {
        // builder.WithOrigins("http://192.168.4.90")
        builder.WithOrigins("http://localhost", "http://localhost:5052")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "-1";

    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Use Serilog for logging
app.UseCors("OnlyOfficePolicy");

app.UseWebSockets();
app.UseRotativa();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// app.UseStatusCodePagesWithRedirects("/StatusCode/{0}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Authentication}/{action=Login}/{id?}");

app.Run();

static async Task ValidateAndInitializeElasticsearchAsync(IElasticClient client)
{
    var pingResponse = await client.PingAsync();
    if (!pingResponse.IsValid)
        throw new Exception("Elasticsearch service is not reachable! Try restarting the service.");

    var health = await client.Cluster.HealthAsync();
    if (health.Status.ToString().Equals("red", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Elasticsearch is not ready. Try restarting the service.");

    var indexExists = await client.Indices.ExistsAsync("documents");
    if (!indexExists.Exists)
    {
        var createIndexResponse = await client.Indices.CreateAsync("documents", c => c
            .Map<DocumentViewModel>(m => m.AutoMap())
        );

        if (!createIndexResponse.IsValid)
            throw new Exception("Failed to initialize elasticsearch properly!");
        else
            Console.WriteLine("'Documents' index created.");
    }
    else
    {
        Console.WriteLine("'Documents' index already exists.");
    }

    var info = await client.RootNodeInfoAsync();
    var version = info.Version.Number;

    if (string.Compare(version, "8.0.0") < 0)
    {
        // On versions older than 8, we can optionally check plugin
        var pluginResponse = await client.Cat.PluginsAsync();
        bool hasAttachmentPlugin = pluginResponse.Records.Any(r => r.Component.Contains("ingest-attachment"));

        if (!hasAttachmentPlugin)
            throw new Exception("Failed to initialize elasticsearch properly!");
    }

    var pipelineResponse = await client.Ingest.GetPipelineAsync(p => p.Id("attachment"));
    if (!pipelineResponse.IsValid || !pipelineResponse.Pipelines.ContainsKey("attachment"))
    {
        var putPipelineResponse = await client.Ingest.PutPipelineAsync("attachment", p => p
            .Description("Extract attachment information")
            .Processors(pr => pr
                .Attachment<DocumentViewModel>(a => a
                    .Field(f => f.Data)
                    .TargetField(f => f.Attachment)
                )
            )
        );

        if (!putPipelineResponse.IsValid)
            throw new Exception("Failed to initialize elasticsearch properly!");
        else
            Console.WriteLine("Attachment pipeline created.");
    }
    else
    {
        Console.WriteLine("Attachment pipeline already exists.");
    }
}