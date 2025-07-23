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
using Serilog;
using Serilog.Sinks.PostgreSQL;
using ElasticFind.Service.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ElasticFindContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Configure Serilog
var columnOptions = new Dictionary<string, ColumnWriterBase>
{
    { "environment_name", new SinglePropertyColumnWriter("environment_name", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "exception", new ExceptionColumnWriter() },
    { "file_path", new SinglePropertyColumnWriter("file_path", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "ip_address", new SinglePropertyColumnWriter("ip_address", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "level", new LevelColumnWriter(true, NpgsqlTypes.NpgsqlDbType.Varchar) },
    { "line_number", new SinglePropertyColumnWriter("line_number", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Integer) },
    { "machine_name", new SinglePropertyColumnWriter("machine_name", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "message", new RenderedMessageColumnWriter() },
    { "message_template", new MessageTemplateColumnWriter() },
    { "method_name", new SinglePropertyColumnWriter("method_name", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "process_info", new SinglePropertyColumnWriter("process_info", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "properties", new PropertiesColumnWriter(NpgsqlTypes.NpgsqlDbType.Jsonb) },
    { "props_test", new SinglePropertyColumnWriter("props_test", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "raise_date", new SinglePropertyColumnWriter("raise_date", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Timestamp) },
    { "thread_id", new SinglePropertyColumnWriter("thread_id", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Integer) },
    { "user_agent", new SinglePropertyColumnWriter("user_agent", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) },
    { "user_name", new SinglePropertyColumnWriter("user_name", PropertyWriteMethod.Raw, NpgsqlTypes.NpgsqlDbType.Text) }
};

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithProcessName()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.PostgreSQL(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        tableName: "logs",
        columnOptions: columnOptions,
        needAutoCreateTable: true)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddScoped<IPreviewFileService, PreviewFileService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

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

string indexName = builder.Configuration["Elasticsearch:IndexName"] ?? "documents";

var pool = new SingleNodeConnectionPool(new Uri(builder.Configuration["Elasticsearch:Url"] ?? "https://localhost:9200"));
var settings = new ConnectionSettings(pool)
    .ServerCertificateValidationCallback((sender, cert, chain, errors) => true) // Ignore cert errors
    .BasicAuthentication(builder.Configuration["Elasticsearch:Username"] ?? "elastic", builder.Configuration["Elasticsearch:Password"] ?? "elastic123") // if password not generated, reset the password in elasticsearch instance
    // .BasicAuthentication("elastic", "xU0dIO7RHrWFwVl-cgb*")
    .DisableDirectStreaming()
    .EnableDebugMode()
    .DefaultIndex(indexName);

var client = new ElasticClient(settings);

// Optional: Register for DI so you can inject IElasticClient later
builder.Services.AddSingleton<IElasticClient>(client);

builder.Services.AddCors(options =>
{
    options.AddPolicy("OnlyOfficePolicy", policyBuilder =>
    {
        // builder.WithOrigins("http://192.168.4.90")
        policyBuilder.WithOrigins(builder.Configuration["OnlyOffice:ServerUrl"] ?? "http://localhost", builder.Configuration["OnlyOffice:ProjectUrl"] ?? "http://localhost:5052")
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

try
{
    await ValidateAndInitializeElasticsearchAsync(client);
}
catch (Exception ex)
{
    StartupDiagnostics.ElasticsearchError = ex.Message;
    Log.Logger.Error(ex.Message);
}

app.Run();

async Task ValidateAndInitializeElasticsearchAsync(IElasticClient client)
{
    var pingResponse = await client.PingAsync();
    if (!pingResponse.IsValid)
        throw new Exception("Elasticsearch service is not reachable! Please start the elasticsearch service.");

    var health = await client.Cluster.HealthAsync();
    if (health.Status.ToString().Equals("red", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Elasticsearch service is not ready. Try restarting the service.");

    var indexExists = await client.Indices.ExistsAsync(indexName);
    if (!indexExists.Exists)
    {
        var createIndexResponse = await client.Indices.CreateAsync(indexName, c => c
            .Map<DocumentViewModel>(m => m.AutoMap())
        );

        if (!createIndexResponse.IsValid)
            throw new Exception("There was some issue initializing Elasticsearch properly! Try restarting the elasticsearch service or the application.");
        else
            Console.WriteLine(indexName + " index created.");
    }
    else
    {
        Console.WriteLine(indexName + " index already exists.");
    }

    var info = await client.RootNodeInfoAsync();
    var version = info.Version.Number;

    if (string.Compare(version, "8.0.0") < 0)
    {
        // On versions older than 8, we can optionally check plugin
        var pluginResponse = await client.Cat.PluginsAsync();
        bool hasAttachmentPlugin = pluginResponse.Records.Any(r => r.Component.Contains("ingest-attachment"));

        if (!hasAttachmentPlugin)
            throw new Exception("Ingest-Attachment plugin is required for processing documents. Please install it to use ElasticFind.");
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
            throw new Exception("There was some issue initializing Elasticsearch properly! Try restarting the application.");
        else
            Console.WriteLine("Attachment pipeline created.");
    }
    else
    {
        Console.WriteLine("Attachment pipeline already exists."); 
    }
}