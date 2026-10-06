using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Controllers;

/// <summary>The owner adds staff logins for this business. Staff can bill and take payments, but not change settings.</summary>
[Authorize(Roles = AppRoles.Owner)]
public class TeamController(MongoContext db, SessionValidator sessions, IValidator<CreateStaffRequest> validator) : StaffApiControllerBase
{
    private static TeamMemberDto ToDto(User u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedAt);

    [HttpGet]
    public async Task<List<TeamMemberDto>> List() =>
        (await db.Users.Find(u => u.BusinessId == BusinessId && u.Role != AppRoles.Client).ToListAsync())
            .OrderBy(u => u.Role != AppRoles.Owner).ThenBy(u => u.Name).Select(ToDto).ToList();

    [HttpPost]
    public async Task<TeamMemberDto> Create(CreateStaffRequest req)
    {
        await validator.ValidateAndThrowAsync(req);
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.Find(u => u.Email == email).AnyAsync())
            throw new AppException("Someone already has an account with this email.", 409);
        var user = new User
        {
            Name = req.Name.Trim(), Email = email, BusinessId = BusinessId, Role = AppRoles.Staff,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };
        await db.Users.InsertOneAsync(user);
        await Log("team.added", "Team", user.Id, $"Added staff login for {user.Name} ({user.Email})");
        return ToDto(user);
    }

    public record ActiveRequest(bool Active);

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest req)
    {
        var u = await Staff(id);
        await db.Users.UpdateOneAsync(x => x.Id == id, Builders<User>.Update
            .Set(x => x.IsActive, req.Active).Inc(x => x.TokenVersion, 1).Set(x => x.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(id);   // switching off ends any open session straight away
        await Log(req.Active ? "team.on" : "team.off", "Team", id, $"{(req.Active ? "Switched on" : "Switched off")} the login of {u.Name}");
        return NoContent();
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, ResetStaffPasswordRequest req)
    {
        if ((req.NewPassword ?? "").Length < 8) throw new AppException("Password must be at least 8 characters.");
        var u = await Staff(id);
        await db.Users.UpdateOneAsync(x => x.Id == id, Builders<User>.Update
            .Set(x => x.PasswordHash, BCrypt.Net.BCrypt.HashPassword(req.NewPassword)).Inc(x => x.TokenVersion, 1).Set(x => x.UpdatedAt, DateTime.UtcNow));
        sessions.Forget(id);
        await Log("team.password", "Team", id, $"Set a new password for {u.Name}");
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var u = await Staff(id);
        await db.Users.DeleteOneAsync(x => x.Id == id);
        sessions.Forget(id);
        await Log("team.deleted", "Team", id, $"Removed the login of {u.Name}");
        return NoContent();
    }

    /// <summary>A staff member of this business. The owner (and yourself) cannot be changed here.</summary>
    private async Task<User> Staff(string id)
    {
        var u = await db.Users.Find(x => x.Id == id && x.BusinessId == BusinessId).FirstOrDefaultAsync() ?? throw new NotFoundException("Staff member");
        if (u.Role != AppRoles.Staff || u.Id == UserId) throw new AppException("Only staff logins can be changed here.");
        return u;
    }
}
