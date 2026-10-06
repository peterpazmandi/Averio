using System.Security.Cryptography;
using System.Text;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Identity;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Households.Invitations;

public sealed class InvitationInputValidator : AbstractValidator<InvitationInput>
{
    public InvitationInputValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254)
        .WithErrorCode("Validation_InvitationEmail");
}

public sealed class InvitationHandler(ICurrentUser currentUser, IIdentityService identity,
    IHouseholdRepository households, IInvitationRepository invitations, IUnitOfWork unitOfWork,
    IInvitationEmailSender emailSender, IValidator<InvitationInput> validator, TimeProvider clock)
{
    private Guid UserId => currentUser.UserId ?? throw new AuthenticationFailedException();
    private static ConflictException Invalid() => new("invitation_invalid", "Error_InvitationInvalid");
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string Normalize(string email) => email.Trim().Normalize().ToUpperInvariant();
    private async Task<Household> OwnerAsync(Guid id, CancellationToken ct)
    {
        var household = await invitations.LockHouseholdAsync(id, ct);
        var member = household?.Members.SingleOrDefault(x => x.UserId == UserId);
        if (member is null) throw new NotFoundException("household", id);
        if (member.Role != HouseholdRole.Owner) throw new ForbiddenException();
        if (!household!.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household;
    }
    public async Task<IReadOnlyList<InvitationView>> ListAsync(Guid householdId, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await OwnerAsync(householdId, ct);
        var rows = await invitations.ListAsync(householdId, ct);
        return rows.Select(x => new InvitationView(x.Id, x.Email,
            x.State == "Pending" && !x.IsPending(clock.GetUtcNow()) ? "Expired" : x.State,
            x.DeliveryStatus, x.Version, x.ExpiresAt, x.LastSentAt)).ToArray();
    }
    public async Task SendAsync(Guid householdId, InvitationInput? input, Guid? invitationId,
        Guid? expectedVersion, CancellationToken ct)
    {
        if (input is not null) await validator.ValidateAndThrowAsync(input, ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        HouseholdInvitation invitation;
        await using (var tx = await unitOfWork.BeginTransactionAsync(ct))
        {
            var household = await OwnerAsync(householdId, ct);
            var now = clock.GetUtcNow();
            var rows = await invitations.ListAsync(householdId, ct);
            if (invitationId is { } id)
            {
                invitation = rows.SingleOrDefault(x => x.Id == id) ?? throw Invalid();
                if (invitation.Version != expectedVersion || invitation.State != "Pending") throw Invalid();
                if (now - invitation.LastSentAt < TimeSpan.FromMinutes(1) || invitation.SendCount >= 10)
                    throw new ConflictException("invitation_throttled", "Error_InvitationThrottled");
                invitation.Rotate(Hash(token), now);
            }
            else
            {
                var email = input!.Email.Trim();
                var normalized = Normalize(email);
                if (rows.Count(x => x.CreatedAt > now.AddDays(-1)) >= 20)
                    throw new ConflictException("invitation_throttled", "Error_InvitationThrottled");
                if (rows.Any(x => x.NormalizedEmail == normalized && x.State == "Pending"))
                    throw new ConflictException("invitation_exists", "Error_InvitationExists");
                foreach (var member in household.Members)
                {
                    var user = await identity.FindByIdAsync(member.UserId, ct);
                    if (user is not null && Normalize(user.Email) == normalized)
                        throw new ConflictException("invitation_member", "Error_InvitationMember");
                }
                invitation = HouseholdInvitation.Create(householdId, UserId, email, normalized, Hash(token), now);
                invitations.Add(invitation);
            }
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        // No raw token is persisted. A restart leaves Unknown, and resend rotates the link.
        var version = invitation.Version;
        var sent = await emailSender.SendAsync(invitation.Email, token, ct);
        await invitations.RecordDeliveryAsync(invitation.Id, version, sent, ct);
    }
    public async Task RevokeAsync(Guid householdId, Guid id, Guid expectedVersion, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await OwnerAsync(householdId, ct);
        var invitation = await invitations.FindAsync(id, ct) ?? throw Invalid();
        if (invitation.HouseholdId != householdId || invitation.Version != expectedVersion || invitation.State != "Pending") throw Invalid();
        invitation.Revoke(clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<InvitationPreview> PreviewAsync(string token, CancellationToken ct)
    {
        var (invitation, user) = await ValidateTokenAsync(token, ct);
        var target = await households.GetByIdForUserAsync(invitation.HouseholdId, invitation.InviterUserId, ct) ?? throw Invalid();
        if (!target.IsFinancialSetupCompleted || !target.Members.Any(x => x.UserId == invitation.InviterUserId && x.Role == HouseholdRole.Owner)) throw Invalid();
        var source = await households.GetByUserIdAsync(user.Id, ct) ?? throw Invalid();
        var joined = invitation.State == "Accepted" && source.Id == target.Id;
        return new(target.Name!, joined, joined || (source.Id != target.Id && await invitations.IsEmptySoloAsync(source, user.Id, ct)));
    }
    public async Task<Guid> AcceptAsync(string token, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        // Serialize acceptances for this identity, then households in deterministic order.
        await invitations.LockUserAsync(UserId, ct);
        var (initial, _) = await ValidateTokenAsync(token, ct);
        var sourceId = (await households.GetMembershipByUserIdAsync(UserId, ct))?.HouseholdId ?? throw Invalid();
        var locked = new Dictionary<Guid, Household>();
        foreach (var id in new[] { sourceId, initial.HouseholdId }.Distinct().Order())
            locked[id] = await invitations.LockHouseholdAsync(id, ct) ?? throw Invalid();
        var (invitation, user) = await ValidateTokenAsync(token, ct);
        var source = locked[sourceId];
        var target = locked[invitation.HouseholdId];
        if (!target.IsFinancialSetupCompleted || !target.Members.Any(x => x.UserId == invitation.InviterUserId && x.Role == HouseholdRole.Owner)) throw Invalid();
        if (invitation.State == "Accepted" && source.Id == target.Id) return target.Id;
        if (source.Id == target.Id || !await invitations.IsEmptySoloAsync(source, user.Id, ct))
            throw new ConflictException("household_not_empty", "Error_HouseholdNotEmpty");
        await invitations.JoinAsync(source, target, user.Id, clock.GetUtcNow(), ct);
        invitation.Accept(user.Id, source.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return target.Id;
    }
    private async Task<(HouseholdInvitation, IdentityUserInfo)> ValidateTokenAsync(string token, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(UserId, ct) ?? throw new AuthenticationFailedException();
        if (!user.EmailConfirmed) throw new EmailNotVerifiedException();
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) throw Invalid();
        var invitation = await invitations.FindByHashAsync(Hash(token), ct) ?? throw Invalid();
        if (invitation.NormalizedEmail != Normalize(user.Email)) throw Invalid();
        if (!invitation.IsPending(clock.GetUtcNow()) && !(invitation.State == "Accepted" && invitation.AcceptedByUserId == user.Id)) throw Invalid();
        return (invitation, user);
    }
}
