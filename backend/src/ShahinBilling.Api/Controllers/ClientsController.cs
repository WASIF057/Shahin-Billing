using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

public class ClientsController(ClientService service, IValidator<Client> validator, PortalAccessService portal) : StaffApiControllerBase
{
    [HttpGet]
    public Task<List<Client>> List([FromQuery] string? search, [FromQuery] bool? active) => service.ListAsync(BusinessId, search, active);

    [HttpGet("{id}")]
    public Task<Client> Get(string id) => service.GetAsync(BusinessId, id);

    [HttpPost]
    public async Task<Client> Create(Client client)
    {
        await validator.ValidateAndThrowAsync(client);
        var created = await service.CreateAsync(BusinessId, client);
        await Log("client.created", "Client", created.Id, $"Added client {created.Name}");
        return created;
    }

    [HttpPut("{id}")]
    public async Task<Client> Update(string id, Client client)
    {
        await validator.ValidateAndThrowAsync(client);
        var updated = await service.UpdateAsync(BusinessId, id, client);
        await Log("client.edited", "Client", id, $"Edited client {updated.Name}");
        return updated;
    }

    public record ActiveRequest(bool Active);

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPatch("{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest req)
    {
        await service.SetActiveAsync(BusinessId, id, req.Active);
        var c = await service.GetAsync(BusinessId, id);
        await Log(req.Active ? "client.shown" : "client.hidden", "Client", id, $"{(req.Active ? "Showed" : "Hid")} client {c.Name} {(req.Active ? "in" : "from")} billing");
        return NoContent();
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var c = await service.GetAsync(BusinessId, id);
        await service.DeleteAsync(BusinessId, id);
        await Log("client.deleted", "Client", id, $"Deleted client {c.Name}");
        return NoContent();
    }

    // ---- ordering login for this client (owner only) ----
    [Authorize(Roles = AppRoles.Owner)]
    [HttpGet("{id}/portal")]
    public Task<PortalAccessDto> GetPortal(string id) => portal.GetAsync(BusinessId, id);

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPost("{id}/portal")]
    public async Task<PortalAccessDto> CreatePortal(string id)
    {
        var r = await portal.CreateAsync(BusinessId, id);
        await Log("portal.created", "Team", id, $"Gave {(await service.GetAsync(BusinessId, id)).Name} an ordering login ({r.Email})");
        return r;
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPost("{id}/portal/invite")]
    public async Task<IActionResult> InvitePortal(string id)
    {
        await portal.InviteAsync(BusinessId, id);
        return NoContent();
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPatch("{id}/portal/active")]
    public async Task<IActionResult> SetPortalActive(string id, ActiveRequest req)
    {
        await portal.SetActiveAsync(BusinessId, id, req.Active);
        await Log(req.Active ? "portal.on" : "portal.off", "Team", id, $"{(req.Active ? "Switched on" : "Switched off")} the ordering login of {(await service.GetAsync(BusinessId, id)).Name}");
        return NoContent();
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpDelete("{id}/portal")]
    public async Task<IActionResult> RemovePortal(string id)
    {
        await portal.RemoveAsync(BusinessId, id);
        await Log("portal.removed", "Team", id, $"Removed the ordering login of {(await service.GetAsync(BusinessId, id)).Name}");
        return NoContent();
    }

    [HttpGet("{id}/special-rates")]
    public Task<List<ClientSpecialRate>> SpecialRates(string id) => service.SpecialRatesAsync(BusinessId, id);
}
