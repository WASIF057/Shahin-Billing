namespace ShahinBilling.Api.Auth;

public class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "ShahinBilling";
    public string Audience { get; set; } = "ShahinBillingWeb";
    public int ExpiryHours { get; set; } = 8;
}
