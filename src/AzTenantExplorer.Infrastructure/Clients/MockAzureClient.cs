using System.Text.Json;
using System.Text.Json.Serialization;
using AzTenantExplorer.Core.Interfaces;
using AzTenantExplorer.Core.Models;

namespace AzTenantExplorer.Infrastructure.Clients;

public class MockAzureClient(string basePath = "MockData") : IAzureTenantClient
{
    private readonly string _basePath = basePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IEnumerable<BillingAccount>> GetBillingAccountsAsync(CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, "billing_accounts.json");
        var response = await ReadJsonFileAsync<BillingAccountResponseDto>(filePath, cancellationToken);

        return response?.Value?.Select(dto => new BillingAccount(
            dto.Id,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.AccountStatus,
            dto.Properties.AgreementType
        )) ?? [];
    }

    public async Task<IEnumerable<BillingProfile>> GetBillingProfilesAsync(string billingAccountId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, "billing_profiles.json");
        var response = await ReadJsonFileAsync<BillingProfileResponseDto>(filePath, cancellationToken);

        return response?.Value?.Select(dto => new BillingProfile(
            dto.Id,
            dto.Properties.SystemId,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.Currency,
            dto.Properties.Status,
            billingAccountId,
            dto.Properties.PoNumber
        )) ?? [];
    }

    public async Task<IEnumerable<InvoiceSection>> GetInvoiceSectionsAsync(string billingAccountId, string billingProfileId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, "invoice_sections.json");
        var response = await ReadJsonFileAsync<InvoiceSectionResponseDto>(filePath, cancellationToken);

        return response?.Value?.Select(dto => new InvoiceSection(
            dto.Id,
            dto.Properties.SystemId,
            dto.Name,
            dto.Properties.DisplayName,
            dto.Properties.State,
            billingProfileId
        )) ?? [];
    }

    public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var armPath = Path.Combine(_basePath, "arm_subscriptions.json");
        var billingPath = Path.Combine(_basePath, "billing_subscriptions.json");

        var armResponse = await ReadJsonFileAsync<ArmSubscriptionResponseDto>(armPath, cancellationToken);
        var billingResponse = File.Exists(billingPath)
            ? await ReadJsonFileAsync<BillingSubscriptionResponseDto>(billingPath, cancellationToken)
            : null;

        // Index commerce offer IDs by Subscription ID
        var commerceOffers = billingResponse?.Value?
            .Where(b => !string.IsNullOrEmpty(b.Properties?.OfferId))
            .ToDictionary(b => b.Properties.SubscriptionId, b => b.Properties.OfferId, StringComparer.OrdinalIgnoreCase)
            ?? [];

        var result = new List<Subscription>();

        if (armResponse?.Value == null) return result;

        foreach (var sub in armResponse.Value)
        {
            var quota = sub.Policies?.QuotaId ?? "Unknown";
            var spendingLimit = sub.Policies?.SpendingLimit ?? "Off";

            // If Commerce API provided an OfferId (MOSP), use it. Otherwise apply Quota Hack (MCA).
            if (!commerceOffers.TryGetValue(sub.SubscriptionId, out var offerId) || string.IsNullOrWhiteSpace(offerId))
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

            result.Add(new Subscription(
                sub.Id,
                sub.SubscriptionId,
                sub.DisplayName,
                sub.State,
                offerId,
                sub.TenantId,
                spendingLimit,
                BillingAccountId: null,
                BillingProfileId: null,
                InvoiceSectionId: null
            ));
        }

        return result;
    }

    private static async Task<T?> ReadJsonFileAsync<T>(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Mock data file missing at: {filePath}");

        using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    // --- Private DTO Envelopes ---
    private class ArmSubscriptionResponseDto { public List<ArmSubDto>? Value { get; set; } }
    private class ArmSubDto
    {
        public string Id { get; set; } = "";
        public string SubscriptionId { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string State { get; set; } = "";
        [JsonPropertyName("subscriptionPolicies")] public SubPoliciesDto? Policies { get; set; }
    }
    private class SubPoliciesDto
    {
        public string QuotaId { get; set; } = "";
        public string SpendingLimit { get; set; } = "";
    }

    private class BillingSubscriptionResponseDto { public List<BillingSubDto>? Value { get; set; } }
    private class BillingSubDto
    {
        public BillingSubProps Properties { get; set; } = new();
    }
    private class BillingSubProps
    {
        public string SubscriptionId { get; set; } = "";
        public string OfferId { get; set; } = "";
    }

    private class BillingAccountResponseDto { public List<AccountDto>? Value { get; set; } }
    private class AccountDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public AccountProps Properties { get; set; } = new();
    }
    private class AccountProps
    {
        public string DisplayName { get; set; } = "";
        public string AccountStatus { get; set; } = "";
        public string AgreementType { get; set; } = "";
    }

    private class BillingProfileResponseDto { public List<ProfileDto>? Value { get; set; } }
    private class ProfileDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ProfileProps Properties { get; set; } = new();
    }
    private class ProfileProps
    {
        public string SystemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Currency { get; set; } = "";
        public string Status { get; set; } = "";
        public string? PoNumber { get; set; }
    }

    private class InvoiceSectionResponseDto { public List<SectionDto>? Value { get; set; } }
    private class SectionDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public SectionProps Properties { get; set; } = new();
    }
    private class SectionProps
    {
        public string SystemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string State { get; set; } = "";
    }
}
