namespace ShahinBilling.Api.Dtos;

public class RegisterRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string BusinessName { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public record UserDto(string Id, string Name, string Email, string Role, string BusinessId, string BusinessName);
public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

/// <summary>Login step 1. Either you are in (Auth is set), or a code was emailed and you finish with /auth/verify-otp.</summary>
public record LoginResponse(bool OtpRequired, string? ChallengeId, string? MaskedEmail, AuthResponse? Auth);
public class VerifyOtpRequest { public string ChallengeId { get; set; } = ""; public string Code { get; set; } = ""; }
public class ResendOtpRequest { public string ChallengeId { get; set; } = ""; }
public class ForgotPasswordRequest { public string Email { get; set; } = ""; }
public class ResetPasswordRequest
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
public class GoogleLoginRequest { public string IdToken { get; set; } = ""; }
public record AuthConfig(string? GoogleClientId);
