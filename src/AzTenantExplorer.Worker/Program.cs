using Azure.Identity;
using Azure.Core;

using AzTenantExplorer.Core.Interfaces;
using AzTenantExplorer.Infrastructure.Clients;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());

builder.Services.AddHttpClient<IAzureTenantClient, AzureTenantClient>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://management.azure.com/");
    });

var host = builder.Build();
host.Run();
