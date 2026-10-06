namespace ShahinBilling.Api.Models;

public class User : Entity
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public string Role { get; set; } = "Owner";
    /// <summary>false = switched off by the owner: cannot log in and any open session ends.</summary>
    /// <summary>For a client's ordering login: which client they are. Empty for owner and staff.</summary>
    public string ClientId { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>Goes up to end every open session of this user (password change, "log out of all devices", switched off).</summary>
    public int TokenVersion { get; set; }
}
