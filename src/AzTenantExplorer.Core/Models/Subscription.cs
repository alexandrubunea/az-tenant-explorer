namespace AzTenantExplorer.Core.Models;

public enum BillingPlatform
{
    Unknown,
    MOSP,
    MCA
}

public record Subscription
(
    string Id,
    string SubscriptionGUID,
    string DisplayName,
    string State,
    string OfferId,
    string TenantGuid,
    string SpendingLimit,

    string? BillingAccountId,
    string? BillingProfileId,
    string? InvoiceSectionName
)
{
    public BillingPlatform GetBillingPlatform =>
        OfferId.EndsWith("P", StringComparison.OrdinalIgnoreCase) ? BillingPlatform.MOSP :
        OfferId.EndsWith("G", StringComparison.OrdinalIgnoreCase) ? BillingPlatform.MCA :
        BillingPlatform.Unknown;

    public bool IsDevTest =>
        OfferId == "MS-AZR-0148G" ||
        OfferId == "MS-AZR-0148P" ||
        OfferId == "MS-AZR-0023P";
}
