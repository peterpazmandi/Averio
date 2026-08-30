namespace Averio.Application.Common.Exceptions;

public abstract class AverioException : Exception
{
    protected AverioException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(resourceKey)
    {
        Code = code;
        ResourceKey = resourceKey;
        Arguments = arguments;
    }

    public string Code { get; }

    public string ResourceKey { get; }

    public object[] Arguments { get; }
}

public sealed class NotFoundException : AverioException
{
    public NotFoundException(
        string resourceName,
        object identifier)
        : base(
            "resource_not_found",
            ErrorResourceKeys.NotFound,
            resourceName,
            identifier)
    {
    }
}

public sealed class ConflictException : AverioException
{
    public ConflictException()
        : base(
            "conflict",
            ErrorResourceKeys.Conflict)
    {
    }
}

public sealed class BusinessRuleException : AverioException
{
    public BusinessRuleException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(code, resourceKey, arguments)
    {
    }
}

public static class ErrorResourceKeys
{
    public const string NotFound = "Error_NotFound";

    public const string Conflict = "Error_Conflict";

    public const string BusinessRule = "Error_BusinessRule";
}