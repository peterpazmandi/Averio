using Averio.Contracts.Transactions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

internal static class ReceiptRequestValidationChecks
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IObjectModelValidator>();
        var transaction = new CreateTransactionRequest(Guid.NewGuid(), "Expense", "Private",
            "Shop", null, new DateOnly(2026, 10, 30),
            [new TransactionItemRequest("Milk", Guid.NewGuid(), "100")], []);
        var request = new PostReceiptRequest(Guid.NewGuid(), "HUF", "100", transaction);

        ModelStateDictionary Validate(RememberReceiptCategoryRequest[]? rules)
        {
            var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
                new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
            // Use MVC's actual record validation strategy, not Validator.TryValidateObject:
            // property-targeted validation on a positional record throws before the action runs.
            validator.Validate(context, null, "", request with { RememberCategoryRules = rules });
            return context.ModelState;
        }

        foreach (var count in new[] { 0, 1, 100 })
            if (!Validate(Enumerable.Range(0, count).Select(i => new RememberReceiptCategoryRequest(i, i)).ToArray()).IsValid)
                throw new Exception($"MVC rejected a receipt request with {count} remembered categories.");
        if (!Validate(null).IsValid)
            throw new Exception("Remembered categories must remain optional.");
        var oversized = Validate(Enumerable.Range(0, 101).Select(i => new RememberReceiptCategoryRequest(i, i)).ToArray());
        if (oversized.IsValid || !oversized.ContainsKey(nameof(PostReceiptRequest.RememberCategoryRules)))
            throw new Exception("MVC must reject more than 100 remembered categories on the correct field.");
    }
}
