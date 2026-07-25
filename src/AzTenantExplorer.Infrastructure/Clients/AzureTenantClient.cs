using Azure.Core;
using AzTenantExplorer.Core.Interfaces;
using AzTenantExplorer.Core.Models;

namespace AzTenantExplorer.Infrastructure.Clients;

public class AzureTenantClient(HttpClient httpClient, TokenCredential credential) : IAzureTenantClient
{
    public Task<IEnumerable<BillingAccount>> GetBillingAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<BillingAccount>());
    }

    public Task<IEnumerable<BillingProfile>> GetBillingProfilesAsync(string billingAccountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<BillingProfile>());
    }

    public Task<IEnumerable<InvoiceSection>> GetInvoiceSectionsAsync(
        string billingAccountId,
        string billingProfileId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<InvoiceSection>());
    }

    public Task<IEnumerable<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<Subscription>());
    }
}
