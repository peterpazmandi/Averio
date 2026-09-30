using Averio.Domain.Transfers;
using Averio.Domain.Accounts;
using FluentValidation;

namespace Averio.Application.Transfers;

public sealed record TransferInput(decimal SentAmount, decimal ReceivedAmount,
    DateOnly OccurredOn, string? Note);

public sealed class TransferInputValidator : AbstractValidator<TransferInput>
{
    public TransferInputValidator()
    {
        RuleFor(x => x.SentAmount).Must(Transfer.IsValidAmount)
            .WithMessage("Validation_TransactionAmountInvalid");
        RuleFor(x => x.ReceivedAmount).Must(Transfer.IsValidAmount)
            .WithMessage("Validation_TransactionAmountInvalid");
        RuleFor(x => x.OccurredOn).NotEmpty().WithMessage("Validation_TransactionDateRequired");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Validation_TransactionNoteTooLong");
    }
}

public sealed record TransferQuery(DateOnly? From, DateOnly? To, Guid? AccountId,
    int PageSize, DateOnly? CursorDate, DateTimeOffset? CursorCreatedAt, Guid? CursorId);

// Each side is nullable: an inaccessible counterparty is never serialized.
public sealed record TransferSide(Guid AccountId, string AccountName, string Currency, decimal Amount);
public sealed record TransferResult(Guid Id, TransferSide? Source, TransferSide? Destination,
    DateOnly OccurredOn, string? Note, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool IsVoided, bool CanModify);
public sealed record TransferPage(IReadOnlyList<TransferResult> Items, bool HasMore);

public interface ITransferRepository
{
    void Add(Transfer transfer);
    // Requires an application transaction; locks are held until commit/rollback.
    Task<IReadOnlyList<Account>> GetLockedAccountsAsync(Guid householdId, Guid sourceId, Guid destinationId, CancellationToken ct);
    Task<Transfer?> GetForUpdateAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    Task<TransferResult?> GetVisibleAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    Task<TransferPage> GetPageAsync(Guid householdId, Guid memberId, TransferQuery query, CancellationToken ct);
}
