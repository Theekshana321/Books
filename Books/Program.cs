using Books.Data.Persistence;
using Books.Data.UnitOfWork;
using Books.Interfaces;
using Books.Domain.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Stripe;
using Books.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// DbContext configuration
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlOptions => sqlOptions.MigrationsAssembly("Books")));

// Bind stripe settings
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));

// Bind EmailSender settings
builder.Services.Configure<EmailSenderSettings>(builder.Configuration.GetSection("EmailSenderSettings"));

// Bind Twilio settings
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection("TwilioSettings"));

// Authentication and authorization
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<SignInManager<ApplicationUser>>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>();

builder.Services.AddAuthentication()
    .AddFacebook(facebookOptions =>
    {
        var facebookAppId = builder.Configuration["FacebookSettings:AppId"];
        var facebookAppSecret = builder.Configuration["FacebookSettings:AppSecret"];

        if (string.IsNullOrEmpty(facebookAppId) || string.IsNullOrEmpty(facebookAppSecret))
        {
            throw new ArgumentException("Facebook AppId and AppSecret must be provided.");
        }

        facebookOptions.AppId = facebookAppId;
        facebookOptions.AppSecret = facebookAppSecret;
    })
    .AddGoogle(googleOptions =>
    {
        var googleClientId = builder.Configuration["GoogleSettings:ClientId"];
        var googleClientSecret = builder.Configuration["GoogleSettings:ClientSecret"];

        if (string.IsNullOrEmpty(googleClientId) || string.IsNullOrEmpty(googleClientSecret))
        {
            throw new ArgumentException("Google ClientId and ClientSecret must be provided.");
        }

        googleOptions.ClientId = googleClientId;
        googleOptions.ClientSecret = googleClientSecret;
    });

// Services configuration
builder.Services.AddScoped<ApplicationUser>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
// Database initializer
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

// Session cache
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(100);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Fixing the error "A possible object cycle was detected"
builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// AutoMapper configuration
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddApiVersioning();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Books", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger(options =>
    {
        options.SerializeAsV2 = true;
    });

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Books API V1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

// Stripe global pipeline
StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

// Invoke function to seed database
SeedDatabase();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
});

app.Run();

// Seed Database
void SeedDatabase()
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    initializer.Initialize();
}
