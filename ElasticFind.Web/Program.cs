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
using Nest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ElasticFindContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    .DefaultIndex("jobs");

var client = new ElasticClient(settings);

// Optional: Register for DI so you can inject IElasticClient later
builder.Services.AddSingleton<IElasticClient>(client);

// Check if index exists and create if not
// var indexExists = client.Indices.Exists("jobs");
// if (!indexExists.Exists)
// {
//     var createIndexResponse = client.Indices.Create("jobs", c => c
//         .Map<Humanresources>(m => m.AutoMap())
//     );

//     if (!createIndexResponse.IsValid)
//     {
//         Console.WriteLine("Failed to create index.");
//         Console.WriteLine($"Debug Info: {createIndexResponse.DebugInformation}");
//         Console.WriteLine($"Server Error: {createIndexResponse.ServerError}");
//     }
//     else
//     {
//         Console.WriteLine("'jobs' index created.");
//     }
// }

// var hrRecord = new Humanresources
// {
//     Nationalidnumber = "IND1234567",
//     LoginID = "0001",
//     Jobtitle = "Software Engineer",
//     Gender = "Male",
// };

// var indexResponse = client.IndexDocument(hrRecord);

// if (indexResponse.IsValid)
// {
//     Console.WriteLine("Document indexed successfully.");
// }
// else
// {
//     Console.WriteLine("Failed to index document.");
//     Console.WriteLine($"Debug Info: {indexResponse.DebugInformation}");
// }

var searchResponse = client.Search<Humanresources>(s => s
    .Query(q => q
        .Match(m => m
            .Field(f => f.Jobtitle)
            .Query("software")
        )
    )
);

foreach (var doc in searchResponse.Documents)
{
    Console.WriteLine($"Found: {doc.Jobtitle} - {doc.Nationalidnumber}");
}

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// app.UseStatusCodePagesWithRedirects("/StatusCode/{0}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Authentication}/{action=Login}/{id?}");

app.Run();
