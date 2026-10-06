using System.Security.Cryptography;
using MongoDB.Driver;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>The owner gives a client an ordering login. The client sets their own password with "Forgot password?", so no password is ever shared.</summary>
public class PortalAccessService(MongoContext db, EmailService email, SessionValidator sessions)
{
    private static PortalAccessDto None => new(false, null, false, null, null);
    private static PortalAccessDto Of(User u, string? warning = null) => new(true, u.Email, u.IsActive, u.CreatedAt, warning);

    private Task<User?> FindAsync(string businessId, string clientId) =>
        db.Users.Find(u => u.BusinessId == businessId && u.ClientId == clientId && u.Role == AppRoles.Client).FirstOrDefaultAsync()!;

    private async Task<Client> ClientAsync(string businessId, string clientId) =>
        await db.Clients.Find(c => c.BusinessId == businessId && c.Id == clientId).FirstOrDefaultAsync() ?? throw new NotFoundException("Client");

    public async Task<PortalAccessDto> GetAsync(string businessId, string clientId)
    {
        await ClientAsync(businessId, clientId);
        var u = await FindAsync(businessId, clientId);
        return u == null ? None : Of(u);
    }

    public async Task<PortalAccessDto> CreateAsync(string businessId, string clientId)
    {
        var client = await ClientAsync(businessId, clientId);
        if (await FindAsync(businessId, clientId) != null) throw new AppException("This client already has an ordering login.", 409);
        if (!EmailService.IsValidEmail(client.Email))
            throw new AppException("Add this client's email address first (Edit client), then give them a login. The invitation goes there.");

        var addr = client.Email.Trim().ToLowerInvariant();
        if (await db.Users.Find(u => u.Email == addr).AnyAsync())
            throw new AppException("Someone already has a login with this email address, so it can't be used for this client.", 409);

        var user = new User
        {
            Name = client.Name, Email = addr, BusinessId = businessId, Role = AppRoles.Client, ClientId = client.Id,
            // A long random password nobody knows: the client chooses their own with "Forgot password?"
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))),
        };
        await db.Users.InsertOneAsync(user);

        try { await email.SendPortalInviteAsync(businessId, client, user); return Of(user); }
        catch (AppException ex) { return Of(user, $"The login was created, but the invitation email could not be sent: {ex.Message} Use \u201cResend invitation\u201d once email works."); }
    }

    public async Task InviteAsync(string businessId, string clientId)
    {
        var client = await ClientAsync(businessId, clientId);
        var user = await FindAsync(businessId, clientId) ?? throw new NotFoundException("Ordering login");
        await email.SendPortalInviteAsync(businessId, client, user);
    }

    public async Task SetActiveAsync(string businessId, string clientId, bool active)
    {
        var user = await FindAsync(businessId, clientId) ?? throw new NotFoundException("Ordering login");
        await db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<User>.Update
            .Set(u => u.IsActive, active).Inc(u => u.TokenVersion, 1).Set(u => u.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(user.Id);   // switching off ends any open session straight away
    }

    public async Task RemoveAsync(string businessId, string clientId)
    {
        var user = await FindAsync(businessId, clientId) ?? throw new NotFoundException("Ordering login");
        await db.Users.DeleteOneAsync(u => u.Id == user.Id);
        sessions.Forget(user.Id);
    }
}
