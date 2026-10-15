using Averio.Application;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Recurring;
using Averio.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddAuthentication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, WorkerCurrentUser>();
builder.Services.AddHostedService<RecurringWorker>();
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
using var host = builder.Build();
if (args.Contains("--verify-services"))
{
    using var scope = host.Services.CreateScope();
    _ = scope.ServiceProvider.GetRequiredService<RecurringHandler>();
    Console.WriteLine("PASS worker dependency graph");
    return;
}
await host.RunAsync();

internal sealed class WorkerCurrentUser : ICurrentUser { public Guid? UserId => null; }
internal sealed class RecurringWorker(IServiceScopeFactory scopes, ILogger<RecurringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Tick(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Recurring batch failed; retrying next minute"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    private async Task Tick(CancellationToken ct)
    {
        IReadOnlyList<RecurringCandidate> candidates;
        await using (var scope = scopes.CreateAsyncScope())
            candidates = await scope.ServiceProvider.GetRequiredService<IRecurringRepository>()
                .DueAsync(DateOnly.FromDateTime(DateTime.UtcNow), 100, ct);
        foreach (var candidate in candidates)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecurringHandler>().AutomaticAsync(candidate, ct);
            }
            catch (Exception ex) when (ex is BusinessRuleException or ForbiddenException or NotFoundException or ConflictException or ValidationException or OnboardingRequiredException)
            {
                // Never log rule names, amounts, account details or exception validation payloads.
                logger.LogWarning("Recurring rule {RuleId} needs review", candidate.RuleId);
                try {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<RecurringHandler>().BlockAsync(candidate, ct);
                }
                catch (NotFoundException) { /* Departed membership: never execute again. */ }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Recurring rule {RuleId} failed; retrying later", candidate.RuleId); }
        }
    }
}
