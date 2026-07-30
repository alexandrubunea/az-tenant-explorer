using Azure.Core;
using AzTenantExplorer.Core.Interfaces;
using AzTenantExplorer.Core.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AzTenantExplorer.Infrastructure.Clients;

public class AzureTenantClient(HttpClient httpClient, TokenCredential credential) : IAzureTenantClient
{
    public Task<IEnumerable<BillingAccount>> GetBillingAccountsAsync(CancellationToken cancellationToken = default)
    {
        string route = "providers/Microsoft.Billing/billingAccounts?api-version=2024-04-01";

        return GetAndMapCollectionAsync<AccountDto, BillingAccount>(route, dto => new BillingAccount(
            dto.Id,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.AccountStatus,
            dto.Properties.AgreementType
        ), cancellationToken);
    }

    public Task<IEnumerable<BillingProfile>> GetBillingProfilesAsync(string billingAccountName, CancellationToken cancellationToken = default)
    {
        string route = $"providers/Microsoft.Billing/billingAccounts/{billingAccountName}/billingProfiles?api-version=2024-04-01";

        return GetAndMapCollectionAsync<BillingDto, BillingProfile>(route, dto => new BillingProfile(
            dto.Id,
            dto.Properties.SystemId,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.Currency,
            dto.Properties.Status,
            billingAccountName,
            dto?.Properties.PoNumber
        ), cancellationToken);
    }

    public Task<IEnumerable<InvoiceSection>> GetInvoiceSectionsAsync(
        string billingAccountName,
        string billingProfileName,
        CancellationToken cancellationToken = default)
    {
        string route = $"https://management.azure.com/providers/Microsoft.Billing/billingAccounts/{billingAccountName}/billingProfiles/{billingProfileName}/invoiceSections?api-version=2024-04-01";

        return GetAndMapCollectionAsync<InvoiceDto, InvoiceSection>(route, dto => new InvoiceSection(
            dto.Id,
            dto.Properties.SystemId,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.State,
            billingProfileName
        ), cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetMCASubscriptionsAsync(
        string billingAccountName,
        CancellationToken cancellationToken = default)
    {
        string armRoute = "subscriptions?api-version=2022-12-01";
        string billingRoute = $"providers/Microsoft.Billing/billingAccounts/{billingAccountName}/billingSubscriptions?api-version=2024-04-01";

        var armTask = GetAndMapCollectionAsync<ArmSubscriptionDto, ArmSubscriptionDto>(
            armRoute, dto => dto, cancellationToken);

        var billingTask = GetAndMapCollectionAsync<BillingSubscriptionDto, BillingSubscriptionDto>(
            billingRoute, dto => dto, cancellationToken);

        await Task.WhenAll(armTask, billingTask);

        var armData = armTask.Result;
        var billingData = billingTask.Result;

        var billingDict = billingData.ToDictionary(
            b => b.Properties.SubscriptionId,
            b => b,
            StringComparer.OrdinalIgnoreCase);

        // Filter ARM subscriptions to only those present in this MCA Billing Account
        return armData
            .Where(arm => billingDict.ContainsKey(arm.SubscriptionId))
            .Select(arm =>
            {
                var bill = billingDict[arm.SubscriptionId];
                var offerId = ResolveMcaOfferId(arm.SubscriptionPolicies?.QuotaId);

                return new Subscription(
                    arm.Id,
                    arm.SubscriptionId,
                    arm.DisplayName,
                    arm.State,
                    offerId,
                    arm.TenantId,
                    arm.SubscriptionPolicies?.SpendingLimit ?? "Unknown",
                    billingAccountName,
                    bill.Properties.BillingProfileName,
                    bill.Properties.InvoiceSectionName
                );
            });
    }

    public async Task<IEnumerable<Subscription>> GetMOSPSubscriptionsAsync(
        IEnumerable<string> knownMcaSubscriptionIds,
        CancellationToken cancellationToken = default)
    {
        string route = "subscriptions?api-version=2022-12-01";

        var mcaSet = new HashSet<string>(knownMcaSubscriptionIds, StringComparer.OrdinalIgnoreCase);
        var armData = await GetAndMapCollectionAsync<ArmSubscriptionDto, ArmSubscriptionDto>(
            route, dto => dto, cancellationToken);

        // Ignore subscriptions already mapped under an MCA Billing Account
        return armData
            .Where(arm => !mcaSet.Contains(arm.SubscriptionId))
            .Select(arm => new Subscription(
                arm.Id,
                arm.SubscriptionId,
                arm.DisplayName,
                arm.State,
                ResolveMospOfferId(arm.SubscriptionPolicies?.QuotaId), // Translator logic applied here
                arm.TenantId,
                arm.SubscriptionPolicies?.SpendingLimit ?? "Unknown",
                null, // No Billing Account available via SPN
                null, // No Billing Profile
                null  // No Invoice Section
            ));
    }

    /* --- Private Methods --- */

    // --- Auth to Azure API
    private async Task AuthenticateRequestAsync(CancellationToken cancellationToken)
    {
        var requestContext = new TokenRequestContext(["https://management.azure.com/.default"]);
        var token = await credential.GetTokenAsync(requestContext, cancellationToken);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    // --- Helper functions
    private static string ResolveMcaOfferId(string? quotaId)
    {
        var quota = quotaId ?? string.Empty;

        // I have not seen a MS-AZR-0148G in a while... I'm not sure if it's worth checking at this moment.
        if (quota.Contains("DevTest", StringComparison.OrdinalIgnoreCase) ||
            quota.Contains("MSDN", StringComparison.OrdinalIgnoreCase))
        {
            return "MS-AZR-0148G";
        }

        return "MS-AZR-0017G";
    }

    private static string ResolveMospOfferId(string? quotaId)
    {
        if (string.IsNullOrWhiteSpace(quotaId))
        {
            return "Unknown-MOSP-Offer";
        }

        // Extract the base string before the underscore (e.g., "PayAsYouGo_2014-09-01" -> "PayAsYouGo")
        var normalizedQuota = quotaId.Split('_')[0];

        // Note: this may be wrong, and also it may need completion. I don't have all the offers for testing.
        return normalizedQuota.ToLowerInvariant() switch
        {
            "payasyougo"       => "MS-AZR-0003P", // Standard Pay-As-You-Go
            "msdndevtest"      => "MS-AZR-0023P", // Pay-As-You-Go Dev/Test
            "msdn"             => "MS-AZR-0059P", // Visual Studio Professional / Enterprise (MSDN)
            "freetrial"        => "MS-AZR-0044P", // Azure Free Trial
            "azureforstudents" => "MS-AZR-0144P", // Azure for Students Starter
            "sponsored"        => "MS-AZR-0036P", // Azure Sponsorship
            _                  => "Unknown-MOSP-Offer"
        };
    }

    // --- Map JSON response from Azure API
    private async Task<IEnumerable<TDomain>> GetAndMapCollectionAsync<TDto, TDomain>(
            string route,
            Func<TDto, TDomain> mapFunc,
            CancellationToken cancellationToken)
    {
        await AuthenticateRequestAsync(cancellationToken);

        var response = await httpClient.GetFromJsonAsync<AzureListResponse<TDto>>(route, cancellationToken);

        return response?.Value?.Select(mapFunc) ?? Enumerable.Empty<TDomain>();
    }

    // --- DTO Records
    private record AzureListResponse<T>(List<T> Value);

    // --- Billing Account DTO
    private record AccountDto(string Id, string Name, AccountProps Properties);
    private record AccountProps(string DisplayName, string AccountStatus, string AgreementType);

    // --- Billing Profile DTO
    private record BillingDto(string Id, string Name, BillingProps Properties);
    private record BillingProps(string SystemId, string DisplayName, string Currency, string Status, string? PoNumber);

    // --- Invoice Section DTO
    private record InvoiceDto(string Id, string Name, InvoiceProps Properties);
    private record InvoiceProps(string DisplayName, string State, string SystemId);

    // --- Subscription DTO
    private record ArmSubscriptionDto(
        string Id,
        string SubscriptionId,
        string DisplayName,
        string State,
        string TenantId,
        ArmPolicies SubscriptionPolicies);
    private record ArmPolicies(string QuotaId, string SpendingLimit);
    private record BillingSubscriptionDto(string Id, BillingSubProps Properties);
    private record BillingSubProps(
        string SubscriptionId,
        string? OfferId,
        string BillingProfileName,
        string InvoiceSectionName);
}
