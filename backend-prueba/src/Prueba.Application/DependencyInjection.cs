using Microsoft.Extensions.DependencyInjection;
using Prueba.Application.Interfaces;
using Prueba.Application.Services;

namespace Prueba.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILoanService, LoanService>();
        return services;
    }
}
