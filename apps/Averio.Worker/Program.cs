using Averio.Worker;
using Averio.Application;
using Averio.Application.Common.Abstractions;
using Averio.Application.Recurring;
using Averio.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddAuthentication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, WorkerCurrentUser>();
builder.Services.AddSingleton<RecurringBatch>();
builder.Services.AddSingleton<RecurringAllowanceBatch>();
builder.Services.AddHostedService<NightlyMaintenanceWorker>();
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
using var host = builder.Build();
if (args.Contains("--verify-services"))
{
    using var scope = host.Services.CreateScope();
    _ = scope.ServiceProvider.GetRequiredService<RecurringHandler>();
    _ = scope.ServiceProvider.GetRequiredService<Averio.Application.PersonalMoney.RecurringAllowanceHandler>();
    _ = scope.ServiceProvider.GetRequiredService<Averio.Application.Receipts.ReceiptProcessor>();
    Console.WriteLine("PASS worker dependency graph");
    return;
}
await host.RunAsync();

internal sealed class WorkerCurrentUser : ICurrentUser { public Guid? UserId => null; }
