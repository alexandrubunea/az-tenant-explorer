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

    public Task<IEnumerable<BillingProfile>> GetBillingProfilesAsync(string billingAccountId, CancellationToken cancellationToken = default)
    {
        string route = $"providers/Microsoft.Billing/billingAccounts/{billingAccountId}/billingProfiles?api-version=2024-04-01";

        return GetAndMapCollectionAsync<BillingDto, BillingProfile>(route, dto => new BillingProfile(
            dto.Id,
            dto.Properities.SystemId,
            dto.Name,
            dto.Properities.DisplayName,
            dto.Properities.Currency,
            dto.Properities.Status,
            billingAccountId,
            dto?.Properities.PoNumber
        ), cancellationToken);
    }

    public Task<IEnumerable<InvoiceSection>> GetInvoiceSectionsAsync(
        string billingAccountId,
        string billingProfileId,
        CancellationToken cancellationToken = default)
    {
        string route = $"https://management.azure.com/providers/Microsoft.Billing/billingAccounts/{billingAccountId}/billingProfiles/{billingProfileId}/invoiceSections?api-version=2024-04-01";

        return GetAndMapCollectionAsync<InvoiceDto, InvoiceSection>(route, dto => new InvoiceSection(
            dto.Id,
            dto.Properities.SystemId,
            dto.Name,
            dto.Properities.DisplayName,
            dto.Properities.State,
            billingProfileId
        ), cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string billingAccount, CancellationToken cancellationToken = default)
    {
        string armRoute = "subscriptions?api-version=2025-04-01";
        string billingRoute = $"/providers/Microsoft.Billing/billingAccounts/{billingAccount}/billingSubscriptions?api-version=2024-04-01";

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

        var mappedSubscriptions = armData.Select(arm =>
        {
            billingDict.TryGetValue(arm.SubscriptionId, out var bill);

            var quota = arm.SubscriptionPolicies?.QuotaId ?? string.Empty;
            var offerId = bill?.Properties.OfferId;

            // If Commerce API provided an OfferId (MOSP), use it. Otherwise apply Quota Hack (MCA).
            if (string.IsNullOrWhiteSpace(offerId))
            {
                if (quota.Contains("DevTest", StringComparison.OrdinalIgnoreCase) ||
                    quota.Contains("MSDN", StringComparison.OrdinalIgnoreCase))
                {
                    offerId = "MS-AZR-0148G"; // MCA Dev/Test
                }
                else
                {
                    offerId = "MS-AZR-0017G"; // MCA Standard
                }
            }

            return new Subscription(
                arm.Id,
                arm.SubscriptionId,
                arm.DisplayName,
                arm.State,
                offerId,
                arm.TenantId,
                arm.SubscriptionPolicies?.SpendingLimit ?? "Unknown",
                bill?.Properties.BillingAccountId,
                bill?.Properties.BillingProfileId,
                bill?.Properties.InvoiceSectionId
            );
        });

        return mappedSubscriptions;
    }

    /* --- Private Methods --- */

    // --- Auth to Azure API
    private async Task AuthenticateRequestAsync(CancellationToken cancellationToken)
    {
        var requestContext = new TokenRequestContext(["https://management.azure.com/.default"]);
        var token = await credential.GetTokenAsync(requestContext, cancellationToken);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
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

    private record AccountDto(string Id, string Name, AccountProps Properties);
    private record AccountProps(string DisplayName, string AccountStatus, string AgreementType);

    private record BillingDto(string Id, string Name, BillingProps Properities);
    private record BillingProps(string SystemId, string DisplayName, string Currency, string Status, string? PoNumber);

    private record InvoiceDto(string Id, string Name, InvoiceProps Properities);
    private record InvoiceProps(string DisplayName, string State, string SystemId);

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
        string BillingAccountId,
        string BillingProfileId,
        string InvoiceSectionId);
}
