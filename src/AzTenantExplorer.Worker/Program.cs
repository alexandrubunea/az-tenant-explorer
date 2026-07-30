using Azure.Identity;
using Azure.Core;

using AzTenantExplorer.Core.Interfaces;
using AzTenantExplorer.Infrastructure.Clients;
using AzTenantExplorer.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());

builder.Services.AddHttpClient<IAzureTenantClient, AzureTenantClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://management.azure.com/");
    });

builder.Services.AddTransient<ConnectionTestWorker>();

var host = builder.Build();

// One-shot CLI test
var worker = host.Services.GetRequiredService<ConnectionTestWorker>();
await worker.RunAsync();
