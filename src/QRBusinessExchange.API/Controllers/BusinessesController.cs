using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Manage businesses (tenants), their members, and their SSO applications.
/// </summary>
[ApiController]
[Route("api/businesses")]
[Authorize]
public class BusinessesController : ControllerBase
{
    private readonly BusinessService _businessService;

    public BusinessesController(BusinessService businessService)
    {
        _businessService = businessService;
    }

    // Businesses ------------------------------------------------------------

    /// <summary>Businesses the signed-in user belongs to.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BusinessDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMine() => WithUser(u => _businessService.GetMyBusinessesAsync(u));

    [HttpGet("{businessId:guid}")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid businessId) => WithUser(u => _businessService.GetBusinessAsync(u, businessId));

    /// <summary>Create a business; the caller becomes its owner.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Create([FromBody] CreateBusinessRequest request) =>
        WithUser(u => _businessService.CreateBusinessAsync(u, request));

    [HttpPut("{businessId:guid}")]
    public Task<IActionResult> Update(Guid businessId, [FromBody] UpdateBusinessRequest request) =>
        WithUser(u => _businessService.UpdateBusinessAsync(u, businessId, request));

    [HttpDelete("{businessId:guid}")]
    public Task<IActionResult> Delete(Guid businessId) =>
        WithUser(async u => { await _businessService.DeleteBusinessAsync(u, businessId); return new { deleted = true }; });

    // Members ---------------------------------------------------------------

    [HttpGet("{businessId:guid}/members")]
    [ProducesResponseType(typeof(List<BusinessMemberDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMembers(Guid businessId) =>
        WithUser(u => _businessService.GetMembersAsync(u, businessId));

    /// <summary>Add an existing account to the business, or change its role.</summary>
    [HttpPost("{businessId:guid}/members")]
    public Task<IActionResult> AddMember(Guid businessId, [FromBody] AddBusinessMemberRequest request) =>
        WithUser(u => _businessService.AddOrUpdateMemberAsync(u, businessId, request));

    [HttpDelete("{businessId:guid}/members/{memberUserId:guid}")]
    public Task<IActionResult> RemoveMember(Guid businessId, Guid memberUserId) =>
        WithUser(async u => { await _businessService.RemoveMemberAsync(u, businessId, memberUserId); return new { removed = true }; });

    // Applications ----------------------------------------------------------

    [HttpGet("{businessId:guid}/applications")]
    [ProducesResponseType(typeof(List<SsoApplicationDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetApplications(Guid businessId) =>
        WithUser(u => _businessService.GetApplicationsAsync(u, businessId));

    /// <summary>Register an application. The client secret is returned only in this response.</summary>
    [HttpPost("{businessId:guid}/applications")]
    [ProducesResponseType(typeof(SsoApplicationDto), StatusCodes.Status200OK)]
    public Task<IActionResult> CreateApplication(Guid businessId, [FromBody] CreateSsoApplicationRequest request) =>
        WithUser(u => _businessService.CreateApplicationAsync(u, businessId, request));

    [HttpPut("{businessId:guid}/applications/{applicationId:guid}")]
    public Task<IActionResult> UpdateApplication(Guid businessId, Guid applicationId, [FromBody] UpdateSsoApplicationRequest request) =>
        WithUser(u => _businessService.UpdateApplicationAsync(u, businessId, applicationId, request));

    /// <summary>Generate a new client secret (the old one stops working immediately).</summary>
    [HttpPost("{businessId:guid}/applications/{applicationId:guid}/rotate-secret")]
    public Task<IActionResult> RotateSecret(Guid businessId, Guid applicationId) =>
        WithUser(u => _businessService.RotateSecretAsync(u, businessId, applicationId));

    [HttpDelete("{businessId:guid}/applications/{applicationId:guid}")]
    public Task<IActionResult> DeleteApplication(Guid businessId, Guid applicationId) =>
        WithUser(async u => { await _businessService.DeleteApplicationAsync(u, businessId, applicationId); return new { deleted = true }; });

    // -----------------------------------------------------------------------

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
