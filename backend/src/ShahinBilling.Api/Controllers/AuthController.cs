using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

public class AuthController(AuthService auth, IConfiguration config, OtpService otp) : ApiControllerBase
{
    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("register")]
    public async Task<LoginResponse> Register(RegisterRequest req, [FromServices] IValidator<RegisterRequest> v)
    {
        await v.ValidateAndThrowAsync(req);
        return await auth.RegisterAsync(req);
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("verify-registration")]
    public Task<AuthResponse> VerifyRegistration(VerifyOtpRequest req) => auth.VerifyRegistrationAsync(req);

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("login")]
    public Task<LoginResponse> Login(LoginRequest req) => auth.LoginAsync(req);

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("verify-otp")]
    public Task<AuthResponse> VerifyOtp(VerifyOtpRequest req) => auth.VerifyOtpAsync(req);

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("resend-otp")]
    public async Task<object> ResendOtp(ResendOtpRequest req) => new { maskedEmail = await otp.ResendAsync(req.ChallengeId) };

    /// <summary>Ends every open session of the logged-in user, on every device.</summary>
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        await auth.LogoutAllAsync(UserId);
        return NoContent();
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("forgot-password")]
    public async Task<object> ForgotPassword(ForgotPasswordRequest req)
    {
        await auth.ForgotPasswordAsync(req.Email);
        return new { message = "If that email has an account, a 6-digit code has been sent to its business email." };
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req, [FromServices] IValidator<ResetPasswordRequest> v)
    {
        await v.ValidateAndThrowAsync(req);
        await auth.ResetPasswordAsync(req);
        return NoContent();
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("google")]
    public Task<AuthResponse> Google(GoogleLoginRequest req) => auth.GoogleLoginAsync(req.IdToken);

    /// <summary>What the login screen needs to know: whether to show the Google button.</summary>
    [AllowAnonymous, HttpGet("config")]
    public AuthConfig GetConfig() => new(string.IsNullOrWhiteSpace(config["Google:ClientId"]) ? null : config["Google:ClientId"]);

    [HttpGet("me")]
    public Task<UserDto> Me() => auth.MeAsync(UserId);

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, [FromServices] IValidator<ChangePasswordRequest> v)
    {
        await v.ValidateAndThrowAsync(req);
        await auth.ChangePasswordAsync(UserId, req);
        return NoContent();
    }
}
