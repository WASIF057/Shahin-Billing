namespace ShahinBilling.Api.Dtos;

public record TeamMemberDto(string Id, string Name, string Email, string Role, bool IsActive, DateTime CreatedAt);
public class CreateStaffRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
public class ResetStaffPasswordRequest { public string NewPassword { get; set; } = ""; }
