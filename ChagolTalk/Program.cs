using ChagolTalk.Data;
using ChagolTalk.Hubs;
using ChagolTalk.Interfaces;
using ChagolTalk.Models.Identity;
using ChagolTalk.Options;
using ChagolTalk.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// DATABASE
// ==========================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ChagolTalkDBContext"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()
    ));

// ==========================================
// IDENTITY
// ==========================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Home/Index";

    // SignalR negotiate requests are XHR, not full-page navigations, so an
    // unauthenticated hub connection must get a 401 instead of a redirect
    // to the login *page* -- otherwise the client sees "200 text/html" and
    // the handshake breaks with a confusing error.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/chatHub"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// ==========================================
// APP SERVICES
// ==========================================

builder.Services.AddSingleton<IMatchingService, MatchingService>();
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddHostedService<QueueJanitorService>();

builder.Services.Configure<TurnServerOptions>(
    builder.Configuration.GetSection(TurnServerOptions.SectionName));

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024;
});

builder.Services.AddControllersWithViews();

// Render (and most PaaS hosts) sit behind a reverse proxy that terminates
// TLS; without this, the app thinks every request is plain HTTP and either
// loops on UseHttpsRedirection or marks the auth cookie as insecure.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Only force HTTPS locally / when not already behind a TLS-terminating
// proxy -- Render's health checks hit the container over plain HTTP, and
// redirecting those breaks deploys.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<ChatHub>("/chatHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
