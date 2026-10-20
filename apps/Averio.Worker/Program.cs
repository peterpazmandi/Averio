using Averio.Worker;
using Averio.Application;
using Averio.Application.Common.Abstractions;
using Averio.Application.Recurring;
using Averio.Infrastructure;
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
builder.Services.AddSingleton<RecurringBatch>();
builder.Services.AddSingleton<RecurringAllowanceBatch>();
builder.Services.AddHostedService<RecurringWorker>();
builder.Services.AddHostedService<RecurringAllowanceWorker>();
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
using var host = builder.Build();
if (args.Contains("--verify-services"))
{
    using var scope = host.Services.CreateScope();
    _ = scope.ServiceProvider.GetRequiredService<RecurringHandler>();
    _ = scope.ServiceProvider.GetRequiredService<Averio.Application.PersonalMoney.RecurringAllowanceHandler>();
    Console.WriteLine("PASS worker dependency graph");
    return;
}
await host.RunAsync();

internal sealed class RecurringAllowanceWorker(
    RecurringAllowanceBatch batch, ILogger<RecurringAllowanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await batch.RunAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Personal Money recurring batch failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

internal sealed class WorkerCurrentUser : ICurrentUser { public Guid? UserId => null; }
internal sealed class RecurringWorker(RecurringBatch batch, ILogger<RecurringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await batch.RunAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Recurring batch failed; retrying next minute"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
