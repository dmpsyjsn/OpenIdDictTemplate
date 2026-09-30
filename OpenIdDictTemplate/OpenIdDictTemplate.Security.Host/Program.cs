using OpenIdDictTemplate.Security.Host.Configuration;
using OpenIdDictTemplate.Security.Host.Infrastructure;
using OpenIdDictTemplate.Security.Logic;
using OpenIdDictTemplate.Security.Logic.Users;

var builder = WebApplication.CreateBuilder(args);

var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>()
    ?? throw new InvalidOperationException($"Missing configuration section '{SecurityOptions.SectionName}'.");
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

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
app.MapControllers();

app.Run();
