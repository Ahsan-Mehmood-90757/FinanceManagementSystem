using Fms.Application.Common;
using Fms.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fms.Api.Infrastructure;

/// <summary>Maps application / domain exceptions to RFC 7807 problem responses.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", "validation"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized", "unauthorized"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", "not_found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", "conflict"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update", "concurrency"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (StatusCodes.Status409Conflict, "Duplicate value", "duplicate"),
            DomainException d => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", d.Code),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "server_error")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception");
        else logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);

        var detail = exception switch
        {
            DbUpdateConcurrencyException => "The record was changed by someone else at the same time. Please retry.",
            DbUpdateException => "A record with the same unique value already exists.",
            _ when status >= 500 => "Something went wrong. The error has been logged.",
            _ => exception.Message
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (exception is ValidationException { Errors.Count: > 0 } v) problem.Extensions["errors"] = v.Errors;

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
