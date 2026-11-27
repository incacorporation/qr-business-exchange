using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;

namespace QRBusinessExchange.API.Controllers;

/// <summary>The signed-in user's cards on file, for fingerprint checkout.</summary>
[ApiController]
[Route("api/saved-cards")]
[Authorize]
public class SavedCardsController : ControllerBase
{
    private readonly SavedCardService _cards;

    public SavedCardsController(SavedCardService cards) => _cards = cards;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavedCardDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List() => Run(userId => _cards.ListAsync(userId));

    /// <summary>Saves a card tokenized by CardPointe's iFrame tokenizer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SavedCardDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Add([FromBody] SaveCardRequest request) => Run(userId => _cards.AddAsync(userId, request));

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Remove(Guid id) => Run(async userId =>
    {
        await _cards.RemoveAsync(userId, id);
        return new { removed = true };
    });

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
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
        catch (QrPaymentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
