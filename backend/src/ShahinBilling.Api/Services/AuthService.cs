using MongoDB.Driver;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class AuthService(MongoContext db, TokenService tokens, OtpService otp, IConfiguration config, SessionValidator sessions, ActivityService activity)
{
    // Checked when the email is unknown, so a wrong email takes as long as a wrong password
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

    /// <summary>Sign-up. While email is set up, nothing is created yet: a code is emailed to the address and the account is made when it is entered.</summary>
    public async Task<LoginResponse> RegisterAsync(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.Find(u => u.Email == email).AnyAsync())
            throw new AppException("An account with this email already exists. Log in instead.", 409);

        var name = req.Name.Trim();
        var businessName = req.BusinessName.Trim();
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        if (!otp.VerifyEmailOnRegister)
            return new LoginResponse(false, null, null, await CreateAccountAsync(name, email, hash, businessName));

        var (challengeId, masked) = await otp.StartRegistrationAsync(name, email, hash, businessName);
        return new LoginResponse(true, challengeId, masked, null);
    }

    /// <summary>Second step of sign-up: the emailed code creates the account.</summary>
    public async Task<AuthResponse> VerifyRegistrationAsync(VerifyOtpRequest req)
    {
        var ch = await otp.VerifyRegistrationAsync(req.ChallengeId, req.Code);
        if (await db.Users.Find(u => u.Email == ch.PendingEmail).AnyAsync())
            throw new AppException("An account with this email already exists. Log in instead.", 409);
        return await CreateAccountAsync(ch.PendingName, ch.PendingEmail, ch.PendingPasswordHash, ch.PendingBusinessName);
    }

    private async Task<AuthResponse> CreateAccountAsync(string name, string email, string passwordHash, string businessName)
    {
        var business = new Business { Name = businessName };
        await db.Businesses.InsertOneAsync(business);
        var user = new User { Name = name, Email = email, PasswordHash = passwordHash, BusinessId = business.Id };
        await db.Users.InsertOneAsync(user);
        return Build(user, business);
    }

    /// <summary>Step 1: password. If email codes are on, a code is emailed and the caller finishes with VerifyOtpAsync.</summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        var ok = BCrypt.Net.BCrypt.Verify(req.Password ?? "", user?.PasswordHash ?? DummyHash);
        if (user == null || !ok) throw new AppException("Email or password is incorrect.", 401);
        if (!user.IsActive) throw new AppException("This login has been switched off. Ask the owner of the business.", 403);

        var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync()
                       ?? throw new NotFoundException("Business");
        if (!otp.IsEnabledFor(business))
        {
            await LogLoginAsync(user, "password");
            return new LoginResponse(false, null, null, Build(user, business));
        }

        var (challengeId, masked) = await otp.StartAsync(user, business);
        return new LoginResponse(true, challengeId, masked, null);
    }

    /// <summary>Emails a reset code. It says nothing about whether the email has an account, so nobody can use it to find out.</summary>
    public async Task ForgotPasswordAsync(string emailInput)
    {
        if (!otp.EmailReady) throw new AppException("Password reset needs email to be connected on this server. Ask whoever set up the app.", 503);
        var email = (emailInput ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user == null) return;
        var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync();
        if (business == null) return;

        // A fresh request within 30 seconds of the last one is ignored (stops email flooding)
        var recent = await db.LoginChallenges.Find(c => c.UserId == user.Id && c.Purpose == "reset" && c.CreatedAt > DateTime.UtcNow.AddSeconds(-30)).AnyAsync();
        if (recent) return;
        try { await otp.StartAsync(user, business, "reset"); }
        catch (AppException) { /* not shown to the caller: that would reveal that the account exists */ }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync()
                   ?? throw new AppException("That code isn't right, or it has expired. Ask for a new one.", 401);
        await otp.VerifyResetAsync(user.Id, req.Code);
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<User>.Update
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(req.NewPassword)).Inc(u => u.TokenVersion, 1).Set(u => u.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(user.Id);
        await db.LoginChallenges.DeleteManyAsync(c => c.UserId == user.Id);   // any pending codes are void now
        await activity.LogAsync(user.BusinessId, user.Id, user.Name, "auth.reset", "Login", user.Id, $"{user.Name} reset their password");
    }

    /// <summary>Step 2: the emailed code.</summary>
    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest req)
    {
        var userId = await otp.VerifyAsync(req.ChallengeId, req.Code);
        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync() ?? throw new NotFoundException("User");
        var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync() ?? throw new NotFoundException("Business");
        if (!user.IsActive) throw new AppException("This login has been switched off. Ask the owner of the business.", 403);
        await LogLoginAsync(user, "email code");
        return Build(user, business);
    }

    private Task LogLoginAsync(User user, string how) =>
        activity.LogAsync(user.BusinessId, user.Id, user.Name, "auth.login", "Login", user.Id, $"{user.Name} logged in ({how})");

    /// <summary>Sign in with Google. Only an existing account can log in: the Google email must match the account email.
    /// Google has already checked who the person is, so no emailed code is asked for.</summary>
    public async Task<AuthResponse> GoogleLoginAsync(string idToken)
    {
        var clientId = config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) throw new AppException("Google sign-in isn't set up.", 404);
        Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken,
                new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
        }
        catch (Google.Apis.Auth.InvalidJwtException)
        {
            throw new AppException("Google sign-in didn't work. Please try again.", 401);
        }
        if (!payload.EmailVerified) throw new AppException("That Google account's email isn't verified.", 401);

        var email = payload.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync()
                   ?? throw new AppException("No account uses this Google email. Log in with your password, or ask for an account for this email.", 401);
        var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync() ?? throw new NotFoundException("Business");
        if (!user.IsActive) throw new AppException("This login has been switched off. Ask the owner of the business.", 403);
        await LogLoginAsync(user, "Google");
        return Build(user, business);
    }

    public async Task<UserDto> MeAsync(string userId)
    {
        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync() ?? throw new NotFoundException("User");
        var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync();
        return ToDto(user, business?.Name ?? "");
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest req)
    {
        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync() ?? throw new NotFoundException("User");
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            throw new AppException("Current password is incorrect.");
        await db.Users.UpdateOneAsync(u => u.Id == userId,
            Builders<User>.Update
                .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(req.NewPassword))
                .Inc(u => u.TokenVersion, 1)   // ends every open session, including this one: log in again
                .Set(u => u.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(userId);
        await activity.LogAsync(user.BusinessId, user.Id, user.Name, "auth.password", "Login", user.Id, $"{user.Name} changed their password");
    }

    /// <summary>Ends every open session of this user on every device (they must log in again).</summary>
    public async Task LogoutAllAsync(string userId)
    {
        var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync() ?? throw new NotFoundException("User");
        await db.Users.UpdateOneAsync(u => u.Id == userId, Builders<User>.Update.Inc(u => u.TokenVersion, 1).Set(u => u.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(userId);
        await activity.LogAsync(user.BusinessId, user.Id, user.Name, "auth.logoutall", "Login", user.Id, $"{user.Name} logged out of all devices");
    }

    private AuthResponse Build(User user, Business business)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, ToDto(user, business.Name));
    }

    private static UserDto ToDto(User u, string businessName) =>
        new(u.Id, u.Name, u.Email, u.Role, u.BusinessId, businessName);
}
