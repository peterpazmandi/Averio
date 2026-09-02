namespace Averio.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}