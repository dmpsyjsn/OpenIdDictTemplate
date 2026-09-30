using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenIdDictTemplate.Security.Logic.Abstractions;

public static class CqrsRegistration
{
    /// <summary>
    /// Registers every handler found in <paramref name="assembly"/> as scoped and wraps it in decorators.
    /// Resolution order: Logging -> UnitOfWork -> handler.
    /// </summary>
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var implementation in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            foreach (var contract in implementation.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                var args = contract.GetGenericArguments();

                if (definition == typeof(IHandleCommandAsync<>))
                    Register(services, contract, implementation, typeof(UnitOfWorkCommandDecorator<>).MakeGenericType(args), typeof(LoggingCommandDecorator<>).MakeGenericType(args));
                else if (definition == typeof(IHandleCommandAsync<,>))
                    Register(services, contract, implementation, typeof(UnitOfWorkCommandDecorator<,>).MakeGenericType(args), typeof(LoggingCommandDecorator<,>).MakeGenericType(args));
                else if (definition == typeof(IHandleQueryAsync<,>))
                    Register(services, contract, implementation, null, typeof(LoggingQueryDecorator<,>).MakeGenericType(args));
            }
        }

        return services;
    }

    private static void Register(IServiceCollection services, Type contract, Type implementation, Type? unitOfWork, Type logging)
    {
        services.AddScoped(contract, sp =>
        {
            object handler = ActivatorUtilities.CreateInstance(sp, implementation);
            if (unitOfWork is not null)
                handler = ActivatorUtilities.CreateInstance(sp, unitOfWork, handler);
            return ActivatorUtilities.CreateInstance(sp, logging, handler);
        });
    }
}
