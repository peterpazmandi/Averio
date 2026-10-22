using System.Diagnostics;
using System.Text.Json;
using Averio.Api.Localization;
using Averio.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Averio.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IStringLocalizer<ErrorMessages> localizer)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validation:
                return await HandleValidationAsync(
                    httpContext,
                    validation);

            case AntiforgeryValidationException:
                return await HandleAntiforgeryAsync(
                    httpContext);
        }

        var response =
            MapException(exception, httpContext.Request.Path.Value ?? string.Empty);

        if (exception is AverioException)
        {
            logger.LogWarning(
                exception,
                "Handled application error {ErrorCode}",
                response.Code);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled API exception");
        }

        var problemDetails =
            new ProblemDetails
            {
                Status =
                    response.StatusCode,

                Title =
                    localizer[
                        response.TitleResourceKey
                    ],

                Detail =
                    response.Detail,

                Instance =
                    httpContext.Request.Path,

                Type =
                    $"https://httpstatuses.com/{response.StatusCode}"
            };

        problemDetails.Extensions["code"] =
            response.Code;

        AddTraceId(
            httpContext,
            problemDetails);

        httpContext.Response.StatusCode =
            response.StatusCode;

        return await WriteAsync(
            httpContext,
            problemDetails);
    }

    private async ValueTask<bool>
        HandleValidationAsync(
            HttpContext httpContext,
            ValidationException exception)
    {
        var errors =
            exception.Errors
                .GroupBy(
                    failure =>
                        JsonNamingPolicy.CamelCase
                            .ConvertName(
                                failure.PropertyName))
                .ToDictionary(
                    group =>
                        group.Key,

                    group =>
                        group
                            .Select(
                                failure =>
                                    localizer[
                                        failure.ErrorCode
                                    ].Value)
                            .Distinct()
                            .ToArray());

        var problem =
            new ValidationProblemDetails(
                errors)
            {
                Status =
                    StatusCodes.Status400BadRequest,

                Title =
                    localizer[
                        "Title_Validation"
                    ],

                Instance =
                    httpContext.Request.Path,

                Type =
                    "https://httpstatuses.com/400"
            };

        problem.Extensions["code"] =
            "validation_failed";

        AddTraceId(
            httpContext,
            problem);

        httpContext.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        return await WriteAsync(
            httpContext,
            problem);
    }

    private async ValueTask<bool>
        HandleAntiforgeryAsync(
            HttpContext httpContext)
    {
        var problem =
            new ProblemDetails
            {
                Status =
                    StatusCodes.Status400BadRequest,

                Title =
                    localizer[
                        "Title_InvalidRequest"
                    ],

                Detail =
                    localizer[
                        "Error_InvalidCsrf"
                    ],

                Instance =
                    httpContext.Request.Path,

                Type =
                    "https://httpstatuses.com/400"
            };

        problem.Extensions["code"] =
            "invalid_csrf_token";

        AddTraceId(
            httpContext,
            problem);

        httpContext.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        return await WriteAsync(
            httpContext,
            problem);
    }

    private ExceptionResponse MapException(
        Exception exception, string requestPath)
    {
        return exception switch
        {
            NotFoundException error =>
                new ExceptionResponse(
                    StatusCodes.Status404NotFound,
                    "Title_NotFound",
                    error.Code,
                    Localize(error)),

            AuthenticationFailedException error =>
                new ExceptionResponse(
                    StatusCodes.Status401Unauthorized,
                    "Title_Unauthorized",
                    error.Code,
                    Localize(error)),

            EmailNotVerifiedException error =>
                new ExceptionResponse(
                    StatusCodes.Status403Forbidden,
                    "Title_Forbidden",
                    error.Code,
                    Localize(error)),

            AccountLockedException error =>
                new ExceptionResponse(
                    423,
                    "Title_Locked",
                    error.Code,
                    Localize(error)),

            InvalidEmailVerificationTokenException error =>
                new ExceptionResponse(
                    StatusCodes.Status400BadRequest,
                    "Title_InvalidRequest",
                    error.Code,
                    Localize(error)),

            ConflictException error =>
                new ExceptionResponse(
                    StatusCodes.Status409Conflict,
                    "Title_Conflict",
                    error.Code,
                    Localize(error)),

            HouseholdRequiredException error =>
                new ExceptionResponse(
                    StatusCodes.Status409Conflict,
                    "Title_HouseholdRequired",
                    error.Code,
                    Localize(error)),

            BusinessRuleException error =>
                new ExceptionResponse(
                    StatusCodes.Status422UnprocessableEntity,
                    "Title_BusinessRule",
                    error.Code,
                    Localize(error)),

            OnboardingRequiredException error =>
                new ExceptionResponse(
                    StatusCodes.Status403Forbidden,
                    "Title_OnboardingRequired",
                    error.Code,
                    Localize(error)),

            InvalidPasswordResetTokenException error =>
                new ExceptionResponse(
                    StatusCodes.Status400BadRequest,
                    "Title_InvalidRequest",
                    error.Code,
                    Localize(error)),

            ForbiddenException error =>
                new ExceptionResponse(
                    StatusCodes.Status403Forbidden,
                    "Title_Forbidden",
                    error.Code,
                    Localize(error)),

            // A development or deployment database may not have had the new
            // Personal Money recurring migration applied yet. Report that specific
            // condition without exposing SQL statements or connection details.
            Exception error when IsRecurringAllowanceSchemaFailure(
                error is DbUpdateException { InnerException: PostgresException inner } ? inner : error,
                requestPath) =>
                new ExceptionResponse(
                    StatusCodes.Status503ServiceUnavailable,
                    "Title_RecurringAllowanceUnavailable",
                    "recurring_allowance_database_update_required",
                    localizer["Error_RecurringAllowanceDatabaseUpdateRequired"]),

            _ =>
                new ExceptionResponse(
                    StatusCodes.Status500InternalServerError,
                    "Title_Unexpected",
                    "unexpected_error",
                    localizer[
                        "Error_Unexpected"
                    ])
        };
    }

    private static bool IsRecurringAllowanceSchemaFailure(Exception exception, string requestPath)
    {
        // Schema-repair messages are limited to the scheduling endpoints.
        // Other SQL errors must retain their original diagnostics and generic 500.
        var segments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (exception is not PostgresException error
            || segments.Length < 6 || segments.Length > 8
            || !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "households", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(segments[3], out _)
            || !string.Equals(segments[4], "personal-money", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[5], "schedules", StringComparison.OrdinalIgnoreCase)
            || (segments.Length >= 7 && !Guid.TryParse(segments[6], out _))
            || (segments.Length == 8 && !string.Equals(segments[7], "history", StringComparison.OrdinalIgnoreCase)))
            return false;

        if (error.SqlState == PostgresErrorCodes.UndefinedTable)
            return string.Equals(error.TableName, "recurring_allowance_rules", StringComparison.Ordinal)
                || (error.TableName is null
                    && error.MessageText == "relation \"recurring_allowance_rules\" does not exist");

        if (error.SqlState != PostgresErrorCodes.UndefinedColumn)
            return false;

        // PostgreSQL 42703 commonly reports the missing column only in MessageText,
        // not ColumnName (for example "column r.creation_amount does not exist").
        if (error.TableName is not null && error.TableName != "recurring_allowance_rules")
            return false;

        if (error.TableName == "recurring_allowance_rules"
            && error.ColumnName is "creation_amount" or "creation_ends_on")
            return true;

        // Match complete known diagnostics, never prefixes of unrelated column names.
        // SELECT uses EF's r alias; INSERT names the relation explicitly.
        return error.MessageText is
            "column r.creation_amount does not exist"
            or "column r.creation_ends_on does not exist"
            or "column \"creation_amount\" of relation \"recurring_allowance_rules\" does not exist"
            or "column \"creation_ends_on\" of relation \"recurring_allowance_rules\" does not exist";
    }

    private string Localize(AverioException exception)
    {
        return localizer[
            exception.ResourceKey,
            exception.Arguments];
    }

    private static void AddTraceId(
        HttpContext context,
        ProblemDetails problemDetails)
    {
        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id
            ?? context.TraceIdentifier;
    }

    private static async ValueTask<bool>
        WriteAsync(
            HttpContext httpContext,
            ProblemDetails problemDetails)
    {
        var service =
            httpContext.RequestServices
                .GetRequiredService<
                    IProblemDetailsService>();

        var written =
            await service.TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext =
                        httpContext,

                    ProblemDetails =
                        problemDetails
                });

        if (!written)
        {
            await httpContext.Response
                .WriteAsJsonAsync(
                    problemDetails);
        }

        return true;
    }

    private sealed record ExceptionResponse(
        int StatusCode,
        string TitleResourceKey,
        string Code,
        string Detail);
}
