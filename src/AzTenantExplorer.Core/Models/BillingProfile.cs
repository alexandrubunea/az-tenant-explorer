namespace AzTenantExplorer.Core.Models;

public record BillingProfile(
    string Id,
    string SystemId,
    string Name,
    string DisplayName,
    string Currency,
    string Status,
    string BillingAccountName,

    string? PoNumber
);
