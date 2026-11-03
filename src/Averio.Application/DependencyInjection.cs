using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Accounts.GetAccount;
using Averio.Application.Accounts.GetAccounts;
using Averio.Application.Accounts.GetAccountOwnerOptions;
using Averio.Application.Onboarding.SaveOnboardingProfile;
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Averio.Application.Onboarding.CompleteOnboarding;
using Averio.Application.Onboarding.GetOnboardingStatus;
using Averio.Application.Households.UpdateHousehold;
using Averio.Application.Identity.ResetPassword;
using Averio.Application.Identity.RequestPasswordReset;
using Averio.Application.Onboarding.SaveOnboardingHousehold;
using Averio.Application.Onboarding.CompleteFirstAccount;
using Averio.Application.Accounts.Common;
using Averio.Application.Accounts.ArchiveAccount;
using Averio.Application.Accounts.RestoreAccount;
using Averio.Application.Dashboard.GetDashboard;

namespace Averio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<Receipts.ReceiptHandler>();
        services.AddScoped<Receipts.PostReceiptHandler>();
        services.AddScoped<Receipts.ReceiptProcessor>();
        services.AddScoped<Recurring.RecurringHandler>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<Households.Invitations.InvitationHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendEmailVerificationHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddScoped<GetOnboardingStatusHandler>();

        services.AddScoped<CompleteOnboardingHandler>();

        services.AddScoped<RequestPasswordResetHandler>();

        services.AddScoped<SaveOnboardingHouseholdHandler>();
        services.AddScoped<SaveOnboardingHouseholdValidator>();

        services.AddScoped<Households.GetHousehold.GetHouseholdHandler>();
        services.AddScoped<Households.Invitations.HouseholdHistoryHandler>();
        services.AddScoped<UpdateHouseholdHandler>();
        services.AddScoped<UpdateHouseholdValidator>();

        services.AddScoped<AccountCreator>();

        services.AddScoped<CompleteFirstAccountHandler>();

        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<ResetPasswordValidator>();

        services.AddScoped<ArchiveAccountHandler>();
        services.AddScoped<RestoreAccountHandler>();

        services.AddScoped<GetDashboardHandler>();

        services.AddScoped<Transactions.SavedViews.SavedViewHandler>();
        services.AddScoped<Activities.ActivityHandler>();
        services.AddScoped<Transfers.TransferHandler>();
        services.AddScoped<PersonalMoney.PersonalMoneyHandler>();
        services.AddScoped<PersonalMoney.RecurringAllowanceHandler>();
        services.AddScoped<Transactions.CreateTransaction.CreateTransactionHandler>();
        services.AddScoped<Transactions.UpdateTransaction.UpdateTransactionHandler>();
        services.AddScoped<Transactions.VoidTransaction.VoidTransactionHandler>();
        services.AddScoped<Transactions.ArchiveTransaction.ArchiveTransactionHandler>();
        services.AddScoped<Transactions.GetTransaction.GetTransactionHandler>();
        services.AddScoped<Transactions.GetTransactions.GetTransactionsHandler>();
        services.AddScoped<Transactions.Common.TransactionReferenceResolver>();
        services.AddScoped<Transactions.Common.TransactionAggregateWriter>();
        services.AddScoped<Statistics.GetCashFlow.GetCashFlowHandler>();
        services.AddScoped<Statistics.GetBoundaryCashFlow.BoundaryCashFlowHandler>();
        services.AddScoped<Statistics.GetCategoryCashFlow.GetCategoryCashFlowHandler>();
        services.AddScoped<Categories.ManageCategories.CategoryManagementHandler>();
        services.AddScoped<Categories.ManageCategories.CategoryPinHandler>();
        services.AddScoped<Categories.GetCategories.GetCategoriesHandler>();
        services.AddScoped<Categories.GetIcons.GetIconsHandler>();
        services.AddScoped<Labels.GetLabels.GetLabelsHandler>();
        services.AddScoped<Labels.CreateLabel.CreateLabelHandler>();

        services.AddScoped<CreateAccountHandler>();
        services.AddScoped<Accounts.UpdateAccount.UpdateAccountHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddScoped<GetAccountsHandler>();
        services.AddScoped<SaveOnboardingProfileHandler>();
        services.AddScoped<HouseholdMemberDirectory>();
        services.AddScoped<GetAccountOwnerOptionsHandler>();

        services.AddScoped<Imports.ImportHandler>();
        return services;
    }
}




