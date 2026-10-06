using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Email one-time codes for login: 6 digits, valid 10 minutes, 5 wrong tries, resend after 30 seconds (3 sends at most).</summary>
public class OtpService(MongoContext db, EmailSender sender, IConfiguration config, IOptions<JwtSettings> jwt, ILogger<OtpService> log)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendWait = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;
    private const int MaxSends = 3;

    /// <summary>Codes are asked for only when the business has it on AND email is set up. (Security:RequireOtp=false in the config switches it off for everyone, as a way back in if email ever breaks.)</summary>
    public bool IsEnabledFor(Business b) =>
        b.RequireLoginOtp && sender.IsConfigured && config.GetValue("Security:RequireOtp", true);

    /// <summary>"wa***@gmail.com": enough to recognise the inbox without showing the address.</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "your email";
        var name = email[..at];
        return name[..Math.Min(2, name.Length)] + "***" + email[at..];
    }

    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private string Hash(string challengeId, string code)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(jwt.Value.Key));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(challengeId + ":" + code)));
    }

    /// <summary>The code goes to the business email (your account email if the business email is empty).</summary>
    private static string Recipient(User user, Business business) =>
        user.Role == AppRoles.Owner && EmailService.IsValidEmail(business.Email) ? business.Email.Trim() : user.Email;   // staff get their own codes

    private async Task SendAsync(string to, string code, string purpose = "login")
    {
        var (subject, body) = purpose switch
        {
            "reset" => ($"Your password reset code: {code}",
                $"Your password reset code is {code}.\n\nIt works for 10 minutes. If you did not ask to reset your password, ignore this email: your password has not changed."),
            "register" => ($"Verify your email: {code}",
                $"Your email verification code is {code}.\n\nIt works for 10 minutes. Enter it to finish creating your account. If you did not sign up, ignore this email: no account is made without this code."),
            _ => ($"Your login code: {code}",
                $"Your login code is {code}.\n\nIt works for 10 minutes. If you did not try to log in, someone may know your password: please change it."),
        };
        try { await sender.SendAsync(new[] { to }, subject, body, null, null); }
        catch (AppException) { throw; }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not send a login code");
            throw new AppException("We couldn't email the code. Check the email settings and try again.", 503);
        }
    }

    /// <summary>True when email is set up, so codes can be sent at all.</summary>
    public bool EmailReady => sender.IsConfigured;

    /// <summary>New accounts must confirm their email with a code, whenever email is set up
    /// (Security:RequireOtp=false switches it off, like the login code).</summary>
    public bool VerifyEmailOnRegister => sender.IsConfigured && config.GetValue("Security:RequireOtp", true);

    /// <summary>Emails a code to the address being signed up. The details are kept (password already hashed) until the code is entered.</summary>
    public async Task<(string ChallengeId, string Masked)> StartRegistrationAsync(string name, string email, string passwordHash, string businessName)
    {
        var code = NewCode();
        var now = DateTime.UtcNow;
        var ch = new LoginChallenge
        {
            Id = ObjectId.GenerateNewId().ToString(), Purpose = "register", ExpiresAt = now + Lifetime, LastSentAt = now,
            PendingName = name, PendingEmail = email, PendingPasswordHash = passwordHash, PendingBusinessName = businessName,
        };
        ch.CodeHash = Hash(ch.Id, code);
        await SendAsync(email, code, "register");
        await db.LoginChallenges.InsertOneAsync(ch);
        return (ch.Id, Mask(email));
    }

    public async Task<(string ChallengeId, string Masked)> StartAsync(User user, Business business, string purpose = "login")
    {
        var to = Recipient(user, business);
        var code = NewCode();
        var now = DateTime.UtcNow;
        var ch = new LoginChallenge { Id = ObjectId.GenerateNewId().ToString(), UserId = user.Id, Purpose = purpose, ExpiresAt = now + Lifetime, LastSentAt = now };
        ch.CodeHash = Hash(ch.Id, code);
        await SendAsync(to, code, purpose);
        if (purpose == "reset") await db.LoginChallenges.DeleteManyAsync(c => c.UserId == user.Id && c.Purpose == "reset");   // one reset code at a time
        await db.LoginChallenges.InsertOneAsync(ch);
        return (ch.Id, Mask(to));
    }

    /// <summary>Checks the code and uses it up. Wrong or expired codes throw a clear message.</summary>
    private async Task<LoginChallenge> VerifyChallengeAsync(string challengeId, string code, string purpose)
    {
        var ch = await db.LoginChallenges.Find(c => c.Id == challengeId).FirstOrDefaultAsync();
        if (ch == null || ch.Purpose != purpose || ch.ExpiresAt < DateTime.UtcNow)
            throw new AppException("That code has expired. Go back and start again to get a new one.", 401);

        if (ch.Attempts >= MaxAttempts)
        {
            await db.LoginChallenges.DeleteOneAsync(c => c.Id == challengeId);
            throw new AppException("Too many wrong codes. Go back and start again to get a new one.", 401);
        }

        var given = Hash(challengeId, (code ?? "").Trim());
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(ch.CodeHash)))
        {
            await db.LoginChallenges.UpdateOneAsync(c => c.Id == challengeId, Builders<LoginChallenge>.Update.Inc(c => c.Attempts, 1));
            var left = MaxAttempts - ch.Attempts - 1;
            throw new AppException(left > 0 ? $"That code isn't right. {left} {(left == 1 ? "try" : "tries")} left." : "Too many wrong codes. Go back and start again to get a new one.", 401);
        }

        await db.LoginChallenges.DeleteOneAsync(c => c.Id == challengeId);   // a code works once
        return ch;
    }

    /// <summary>Returns the user id when the code is right.</summary>
    public async Task<string> VerifyAsync(string challengeId, string code, string purpose = "login") =>
        (await VerifyChallengeAsync(challengeId, code, purpose)).UserId;

    /// <summary>Returns the waiting sign-up details when the emailed code is right.</summary>
    public Task<LoginChallenge> VerifyRegistrationAsync(string challengeId, string code) =>
        VerifyChallengeAsync(challengeId, code, "register");

    /// <summary>Checks a password-reset code for this user. Returns when it is right; otherwise throws.</summary>
    public async Task VerifyResetAsync(string userId, string code)
    {
        var ch = await db.LoginChallenges.Find(c => c.UserId == userId && c.Purpose == "reset")
            .SortByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (ch == null) throw new AppException("That code isn't right, or it has expired. Ask for a new one.", 401);
        await VerifyAsync(ch.Id, code, "reset");
    }

    public async Task<string> ResendAsync(string challengeId)
    {
        var ch = await db.LoginChallenges.Find(c => c.Id == challengeId && (c.Purpose == "login" || c.Purpose == "register")).FirstOrDefaultAsync();
        if (ch == null || ch.ExpiresAt < DateTime.UtcNow) throw new AppException("That code has expired. Go back and start again.", 401);
        if (ch.Sends >= MaxSends) throw new AppException("No more codes can be sent this time. Go back and start again.", 429);
        if (DateTime.UtcNow - ch.LastSentAt < ResendWait) throw new AppException("Please wait a few seconds before asking for another code.", 429);

        string to;
        if (ch.Purpose == "register") to = ch.PendingEmail;
        else
        {
            var user = await db.Users.Find(u => u.Id == ch.UserId).FirstOrDefaultAsync() ?? throw new NotFoundException("User");
            var business = await db.Businesses.Find(b => b.Id == user.BusinessId).FirstOrDefaultAsync() ?? throw new NotFoundException("Business");
            to = Recipient(user, business);
        }
        var code = NewCode();
        await SendAsync(to, code, ch.Purpose);
        await db.LoginChallenges.UpdateOneAsync(c => c.Id == challengeId, Builders<LoginChallenge>.Update
            .Set(c => c.CodeHash, Hash(challengeId, code)).Set(c => c.Attempts, 0).Set(c => c.LastSentAt, DateTime.UtcNow)
            .Set(c => c.ExpiresAt, DateTime.UtcNow + Lifetime).Inc(c => c.Sends, 1));
        return Mask(to);
    }
}
