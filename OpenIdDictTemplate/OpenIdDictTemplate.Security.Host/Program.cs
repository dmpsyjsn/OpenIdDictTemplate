using OpenIdDictTemplate.Security.Host.Configuration;
using OpenIdDictTemplate.Security.Host.Infrastructure;
using OpenIdDictTemplate.Security.Logic;
using OpenIdDictTemplate.Security.Logic.Users;
using System.Threading.RateLimiting;
using OpenIdDictTemplate.Security.Host.Common;

var builder = WebApplication.CreateBuilder(args);

var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
    ?? throw new InvalidOperationException($"Missing configuration section '{SecurityOptions.SectionName}'.");
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(CommonConstants.LoginLimiter, context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{CommonConstants.LoginLimiter}_{ip}",
            _ => new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
    });

    options.AddPolicy(CommonConstants.StrictLimiter, context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{CommonConstants.StrictLimiter}_{ip}",
            _ => new() { PermitLimit = 3, Window = TimeSpan.FromMinutes(15) });
    });
});

builder.Services.AddControllersWithViews();

builder.Services.AddSecurityLogic(builder.Configuration, builder.Environment);
builder.Services.AddSecurityServer(builder.Configuration, securityOptions);
builder.Services.AddScoped<IAccountLinkBuilder, AccountLinkBuilder>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");

app.UseStatusCodePagesWithReExecute("/error");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors(OpenIddictServerSetup.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
