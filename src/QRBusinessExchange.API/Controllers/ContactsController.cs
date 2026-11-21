using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// The signed-in user's contacts: people saved by scanning their business card.
/// </summary>
[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly ContactService _contacts;

    public ContactsController(ContactService contacts)
    {
        _contacts = contacts;
    }

    /// <summary>All contacts, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ContactDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll() => WithUser(u => _contacts.GetContactsAsync(u));

    /// <summary>Whether the person is in your contacts.</summary>
    [HttpGet("{userId:guid}")]
    public Task<IActionResult> IsContact(Guid userId) =>
        WithUser(async u => new { isContact = await _contacts.IsContactAsync(u, userId) });

    /// <summary>Add a person (from their business-card QR code). Adding someone twice is fine.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Add([FromBody] AddContactRequest request) =>
        WithUser(u => _contacts.AddContactAsync(u, request.UserId));

    [HttpDelete("{userId:guid}")]
    public Task<IActionResult> Remove(Guid userId) =>
        WithUser(async u => { await _contacts.RemoveContactAsync(u, userId); return new { deleted = true }; });

    private async Task<IActionResult> WithUser<T>(Func<Guid, Task<T>> action)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value));
        }
        catch (SsoException ex)
        {
            return StatusCode(ex.StatusCode, new SsoErrorResponse { Error = ex.Error, ErrorDescription = ex.Message });
        }
    }
}
