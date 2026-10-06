namespace ShahinBilling.Api.Auth;

/// <summary>Owner: everything. Staff: bills, payments and clients only (no settings, reports, setup, cancelling or deleting).</summary>
public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
    /// <summary>A client of the business with an ordering login. Sees product names (never prices) and their own orders only.</summary>
    public const string Client = "Client";
}
