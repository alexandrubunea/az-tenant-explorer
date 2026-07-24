namespace AzTenantExplorer.Core.Models;

public record BillingAccount(
    string Id,
    string Name,
    string DisplayName,
    string AccountStatus,
    string AgreementType
)
{
    public BillingPlatform GetBillingPlatform =>
        string.Equals(AgreementType, "MicrosoftCustomerAgreement", StringComparison.OrdinalIgnoreCase) ? BillingPlatform.MCA :
        string.Equals(AgreementType, "MicrosoftOnlineServicesProgram", StringComparison.OrdinalIgnoreCase) ? BillingPlatform.MOSP :
        BillingPlatform.Unknown;
}
