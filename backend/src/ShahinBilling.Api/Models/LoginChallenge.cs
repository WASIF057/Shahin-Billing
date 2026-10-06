namespace ShahinBilling.Api.Models;

/// <summary>A pending "enter the code we emailed you" step of a login. Deleted when used; expired ones clean themselves up.</summary>
public class LoginChallenge
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    /// <summary>"login" (finish a login), "reset" (choose a new password) or "register" (confirm a new account's email). A code for one can never be used for the other.</summary>
    public string Purpose { get; set; } = "login";
    /// <summary>The code is never stored, only a keyed hash of it.</summary>
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    // For Purpose "register": the sign-up details wait here. No account exists until the emailed code is entered.
    public string PendingName { get; set; } = "";
    public string PendingEmail { get; set; } = "";
    public string PendingPasswordHash { get; set; } = "";
    public string PendingBusinessName { get; set; } = "";
    public int Attempts { get; set; }
    public int Sends { get; set; } = 1;
    public DateTime LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
