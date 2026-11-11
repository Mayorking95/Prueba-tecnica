using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Prueba.Domain.Exceptions;

namespace Prueba.Api.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acceso denegado"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "No autenticado"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio incumplida"),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Petición cancelada"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error no controlado en {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "Ocurrió un error inesperado."
                : exception.Message,
            Instance = context.Request.Path
        }, ct);

        return true;
    }
}
