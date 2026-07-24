namespace AzTenantExplorer.Core.Models;

public record InvoiceSection(
  string Id,
  string SystemId,
  string Name,
  string DisplayName,
  string State,
  string BillingProfileId
);
