using AzTenantExplorer.Core.Models;
using AzTenantExplorer.Core.Interfaces;

namespace AzTenantExplorer.Worker;

public class ConnectionTestWorker(IAzureTenantClient client)
{
    public async Task RunAsync()
    {
        Console.WriteLine("=== AzTenantExplorer Connection Test ===\n");

        try
        {
            await TestBillingAccountsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL] Connection test failed: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException is not null)
                Console.WriteLine($"        Inner: {ex.InnerException.Message}");
        }

        Console.WriteLine("\n=== Test Complete ===");
    }

    private async Task TestBillingAccountsAsync()
    {
        Console.WriteLine("[1] Fetching Billing Accounts...");
        var accounts = (await client.GetBillingAccountsAsync()).ToList();

        if (accounts.Count == 0)
        {
            Console.WriteLine("    No billing accounts found. Check Service Principal permissions.");
            return;
        }

        Console.WriteLine($"    Found {accounts.Count} billing account(s):\n");

        foreach (var account in accounts)
        {
            Console.WriteLine($"  - {account.DisplayName} ({account.Name})");
            Console.WriteLine($"      Status: {account.AccountStatus} | Agreement: {account.AgreementType} | Platform: {account.GetBillingPlatform}");

            if (account.GetBillingPlatform == BillingPlatform.MCA)
            {
                await TestBillingProfilesAsync(account.Name);
            }
            else
            {
                Console.WriteLine("      (Skipping profile/invoice lookup - not MCA)");
            }

            Console.WriteLine();
        }

        // Subscriptions (both MCA and MOSP), using the first MCA account found (if any)
        var mcaAccount = accounts.FirstOrDefault(a => a.GetBillingPlatform == BillingPlatform.MCA);
        var knownMcaSubIds = new List<string>();

        if (mcaAccount is not null)
        {
            Console.WriteLine($"[4] Fetching MCA Subscriptions for '{mcaAccount.DisplayName}'...");
            var mcaSubs = (await client.GetMCASubscriptionsAsync(mcaAccount.Name)).ToList();
            knownMcaSubIds = mcaSubs.Select(s => s.SubscriptionGUID).ToList();
            PrintSubscriptions(mcaSubs);
        }

        Console.WriteLine("\n[5] Fetching MOSP (legacy/unmapped) Subscriptions...");
        var mospSubs = (await client.GetMOSPSubscriptionsAsync(knownMcaSubIds)).ToList();
        PrintSubscriptions(mospSubs);
    }

    private async Task TestBillingProfilesAsync(string billingAccountName)
    {
        Console.WriteLine("      [2] Fetching Billing Profiles...");
        var profiles = (await client.GetBillingProfilesAsync(billingAccountName)).ToList();

        if (profiles.Count == 0)
        {
            Console.WriteLine("          No billing profiles found.");
            return;
        }

        foreach (var profile in profiles)
        {
            Console.WriteLine($"          - {profile.DisplayName} ({profile.Name}) | Currency: {profile.Currency} | Status: {profile.Status}");
            await TestInvoiceSectionsAsync(billingAccountName, profile.Name);
        }
    }

    private async Task TestInvoiceSectionsAsync(string billingAccountName, string billingProfileName)
    {
        Console.WriteLine("              [3] Fetching Invoice Sections...");
        var sections = (await client.GetInvoiceSectionsAsync(billingAccountName, billingProfileName)).ToList();

        if (sections.Count == 0)
        {
            Console.WriteLine("                  No invoice sections found.");
            return;
        }

        foreach (var section in sections)
        {
            Console.WriteLine($"                  - {section.DisplayName} ({section.Name}) | State: {section.State}");
        }
    }

    private static void PrintSubscriptions(List<Subscription> subs)
    {
        if (subs.Count == 0)
        {
            Console.WriteLine("    None found.");
            return;
        }

        foreach (var sub in subs)
        {
            Console.WriteLine($"  - {sub.DisplayName} ({sub.SubscriptionGUID})");
            Console.WriteLine($"      State: {sub.State} | Offer: {sub.OfferId} | Platform: {sub.GetBillingPlatform} | DevTest: {sub.IsDevTest}");
            Console.WriteLine($"      BillingAccount: {sub.BillingAccountId ?? "N/A"} | Profile: {sub.BillingProfileId ?? "N/A"} | InvoiceSection: {sub.InvoiceSectionName ?? "N/A"}");
        }
    }
}
