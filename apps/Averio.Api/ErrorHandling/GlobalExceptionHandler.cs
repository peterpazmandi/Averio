using System.Diagnostics;
using Averio.Api.Localization;
using Averio.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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
        var response = MapException(exception);

        if (exception is AverioException)
        {
            logger.LogWarning(
                exception,
                "Handled application exception {ErrorCode}",
                response.Code);
        }
        else
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing request");
        }

        var problemDetails = new ProblemDetails
        {
            Status = response.StatusCode,
            Title = localizer[response.TitleResourceKey],
            Detail = response.Detail,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{response.StatusCode}"
        };

        problemDetails.Extensions["code"] = response.Code;

        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id
            ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = response.StatusCode;

        var problemDetailsService =
            httpContext.RequestServices
                .GetRequiredService<IProblemDetailsService>();

        var written = await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);
        }

        return true;
    }

    private ExceptionResponse MapException(Exception exception)
    {
        return exception switch
        {
            NotFoundException notFound =>
                new ExceptionResponse(
                    StatusCodes.Status404NotFound,
                    "Title_NotFound",
                    notFound.Code,
                    Localize(notFound)),

            ConflictException conflict =>
                new ExceptionResponse(
                    StatusCodes.Status409Conflict,
                    "Title_Conflict",
                    conflict.Code,
                    Localize(conflict)),

            BusinessRuleException businessRule =>
                new ExceptionResponse(
                    StatusCodes.Status422UnprocessableEntity,
                    "Title_BusinessRule",
                    businessRule.Code,
                    Localize(businessRule)),

            _ =>
                new ExceptionResponse(
                    StatusCodes.Status500InternalServerError,
                    "Title_Unexpected",
                    "unexpected_error",
                    localizer["Error_Unexpected"])
        };
    }

    private string Localize(AverioException exception)
    {
        return localizer[
            exception.ResourceKey,
            exception.Arguments];
    }

    private sealed record ExceptionResponse(
        int StatusCode,
        string TitleResourceKey,
        string Code,
        string Detail);
}