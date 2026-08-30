using NovaFE.Application.Common;
using NovaFE.Application.Signing;
using NovaFE.Application.Signing.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Application;

public static class ApplicationService
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationService).Assembly;

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // Servicios de aplicación (orquestación que no es un caso de uso de cara al usuario).
        services.AddScoped<ICertificateSigner, CertificateSigner>();

        // Auto-register all use cases that implement IUseCase<,>
        var useCaseTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IUseCase<,>)));

        foreach (var type in useCaseTypes)
        {
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IUseCase<,>));

            foreach (var iface in interfaces)
                services.AddScoped(iface, type);

            // Also register as the concrete type so controllers can inject directly
            services.AddScoped(type);
        }


        return services;
    }
}