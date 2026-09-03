using Microsoft.AspNetCore.Antiforgery;

namespace Averio.Api.Security;

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var requiresValidation =
            context.Request.Path.StartsWithSegments("/api/v1")
                && IsUnsafeMethod(context.Request.Method);

        if (requiresValidation)
        {
            await antiforgery.ValidateRequestAsync(context);
        }

        await next(context);
    }

    private static bool IsUnsafeMethod(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}