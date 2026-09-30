using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIdDictTemplate.Security.Logic.Abstractions;
using OpenIdDictTemplate.Security.Logic.Data;
using OpenIdDictTemplate.Security.Logic.Email;

namespace OpenIdDictTemplate.Security.Logic;

public static class SecurityLogicRegistration
{
    /// <summary>Single registration entry point for everything in the Logic project.</summary>
    public static IServiceCollection AddSecurityLogic(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSecurityPersistence(configuration, environment.ContentRootPath);

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var emailType = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()?.Type ?? EmailType.PickupDirectory;
        if (emailType == EmailType.Smtp)
            services.AddScoped<IEmailTransport, SmtpEmailTransport>();
        else
            services.AddScoped<IEmailTransport, PickupDirectoryEmailTransport>();
        services.AddScoped<ISecurityEmailSender, SecurityEmailSender>();

        services.AddCqrsHandlers(typeof(SecurityLogicRegistration).Assembly);

        services.AddScoped<IdentitySeeder>();
        services.AddScoped<OpenIddictSeeder>();
        services.AddHostedService<SecurityDataInitializer>();

        return services;
    }
}
