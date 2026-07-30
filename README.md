# AZ Tenant Explorer
AzTenantExplorer is a utility designed to give Azure administrators a clear, structured, and hierarchical view of their Azure Tenant's billing and subscription landscape.

_Note: The development of this project is completely independent of Microsoft. It strictly utilizes standard, publicly available Microsoft REST APIs to aggregate and display data._

# Limitations & Prerequisites
- **Initial Setup Required**: This tool cannot run instantly out-of-the-box. To connect AzTenantExplorer to your tenant, you must first configure a Service Principal (App Registration) in Microsoft Entra ID and inject its credentials into your environment variables.
- **MCA Architecture is Recommended**: Because Azure's modern billing APIs rely heavily on Role-Based Access Control (RBAC), this tool is optimized for modern Microsoft Customer Agreements (MCA). For full visibility, you must assign the Billing Reader role to the tool's Service Principal at the root level of your MCA Billing Account.
  - **Best Practice**: Microsoft recommends consolidating under a single MCA billing account managed by a dedicated service email address (rather than personal accounts) to centralize costs and simplify RBAC governance.
- **Limited MOSP Support**: Support for legacy Microsoft Online Services Program (MOSP / Pay-As-You-Go) subscriptions is intentionally limited. Microsoft is actively transitioning away from MOSP. Crucially, MOSP billing accounts do not support the modern RBAC assignments required for background automation.
- **Invoice Visibility Constraints**: Because of the MOSP RBAC limitations mentioned above, AzTenantExplorer can only map MOSP resources via the standard ARM API. It cannot retrieve or map invoice data for MOSP accounts (legacy invoices, which often feature an "E" prefix in their invoice number). For missing or historical MOSP invoices, administrators must log into the Azure Portal manually using the credentials of the original Account Administrator.
- **Not a Recovery Tool**: AzTenantExplorer is not designed to magically locate missing invoices or bypass Azure security boundaries. It is an aggregation engine designed to help you visualize the complex relationships between your Billing Accounts, Billing Profiles, Invoice Sections, and Subscriptions.
