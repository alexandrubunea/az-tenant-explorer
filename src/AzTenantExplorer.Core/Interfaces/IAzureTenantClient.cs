using AzTenantExplorer.Core.Models;

namespace AzTenantExplorer.Core.Interfaces;

public interface IAzureTenantClient
{
    Task<IEnumerable<Subscription>> GetMOSPSubscriptionsAsync(IEnumerable<string> knownMcaSubscriptionIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<Subscription>> GetMCASubscriptionsAsync(string billingAccountName, CancellationToken cancellationToken = default)
    Task<IEnumerable<BillingAccount>> GetBillingAccountsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BillingProfile>> GetBillingProfilesAsync(string billingAccountId, CancellationToken cancellationToken = default);
    Task<IEnumerable<InvoiceSection>> GetInvoiceSectionsAsync(string billingAccountId, string billingProfileId, CancellationToken cancellationToken = default);
}
