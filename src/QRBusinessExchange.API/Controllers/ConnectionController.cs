using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for managing business connections between users
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConnectionController : ControllerBase
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<ConnectionController> _logger;

    public ConnectionController(
        IConnectionRepository connectionRepository,
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<ConnectionController> logger)
    {
        _connectionRepository = connectionRepository;
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Initiate a new connection request
    /// </summary>
    /// <param name="request">Connection request details</param>
    /// <returns>Created connection</returns>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(ConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateConnection([FromBody] ConnectionRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId == request.RecipientUserId)
        {
            return BadRequest(new { error = "Cannot connect with yourself" });
        }

        // Check if recipient exists
        var recipient = await _userRepository.GetByIdAsync(request.RecipientUserId);
        if (recipient == null)
        {
            return NotFound(new { error = "Recipient user not found" });
        }

        // Check if user has credits
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        if (subscription == null || subscription.CreditsAvailable <= 0)
        {
            return BadRequest(new { error = "Insufficient credits to initiate connection" });
        }

        // Create connection
        var connection = new Connection
        {
            InitiatorUserId = userId,
            RecipientUserId = request.RecipientUserId,
            Status = "Pending",
            Message = request.Message,
            ConnectionDate = DateTime.UtcNow,
            CreditDeducted = false
        };

        var createdConnection = await _connectionRepository.CreateAsync(connection);

        // Deduct credit
        subscription.CreditsAvailable--;
        subscription.CreditsUsed++;
        await _subscriptionRepository.UpdateAsync(subscription);

        // Update connection
        createdConnection.CreditDeducted = true;
        await _connectionRepository.UpdateStatusAsync(createdConnection.ConnectionId, "Pending");

        return CreatedAtAction(nameof(InitiateConnection), MapToConnectionDto(createdConnection));
    }

    /// <summary>
    /// Get pending connection requests
    /// </summary>
    /// <returns>List of pending connections</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<ConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingConnections()
    {
        var userId = GetCurrentUserId();
        var connections = await _connectionRepository.GetPendingByUserIdAsync(userId);
        
        var connectionDtos = new List<ConnectionDto>();
        foreach (var connection in connections)
        {
            var dto = MapToConnectionDto(connection);
            
            // Load user details
            if (connection.InitiatorUserId != userId)
            {
                var initiator = await _userRepository.GetByIdAsync(connection.InitiatorUserId);
                if (initiator != null)
                {
                    dto.InitiatorUser = MapToUserDto(initiator);
                }
            }
            
            if (connection.RecipientUserId != userId)
            {
                var recipient = await _userRepository.GetByIdAsync(connection.RecipientUserId);
                if (recipient != null)
                {
                    dto.RecipientUser = MapToUserDto(recipient);
                }
            }
            
            connectionDtos.Add(dto);
        }

        return Ok(connectionDtos);
    }

    /// <summary>
    /// Accept a connection request
    /// </summary>
    /// <param name="id">Connection ID</param>
    /// <returns>Updated connection</returns>
    [HttpPut("{id}/accept")]
    [ProducesResponseType(typeof(ConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptConnection(Guid id)
    {
        var userId = GetCurrentUserId();
        var connection = await _connectionRepository.GetByIdAsync(id);
        
        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (connection.RecipientUserId != userId)
        {
            return Forbid();
        }

        if (connection.Status != "Pending")
        {
            return BadRequest(new { error = "Connection is not pending" });
        }

        var updatedConnection = await _connectionRepository.UpdateStatusAsync(id, "Accepted");
        
        return Ok(MapToConnectionDto(updatedConnection));
    }

    /// <summary>
    /// Reject a connection request
    /// </summary>
    /// <param name="id">Connection ID</param>
    /// <returns>Updated connection</returns>
    [HttpPut("{id}/reject")]
    [ProducesResponseType(typeof(ConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectConnection(Guid id)
    {
        var userId = GetCurrentUserId();
        var connection = await _connectionRepository.GetByIdAsync(id);
        
        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (connection.RecipientUserId != userId)
        {
            return Forbid();
        }

        if (connection.Status != "Pending")
        {
            return BadRequest(new { error = "Connection is not pending" });
        }

        var updatedConnection = await _connectionRepository.UpdateStatusAsync(id, "Rejected");
        
        return Ok(MapToConnectionDto(updatedConnection));
    }

    /// <summary>
    /// Get connection history
    /// </summary>
    /// <returns>List of all connections</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<ConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory()
    {
        var userId = GetCurrentUserId();
        var connections = await _connectionRepository.GetHistoryAsync(userId);
        
        return Ok(connections.Select(MapToConnectionDto));
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = HttpContext.Items["UserId"];
        if (userIdClaim != null && userIdClaim is Guid userId)
        {
            return userId;
        }

        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("sub");
        
        if (claim != null && Guid.TryParse(claim.Value, out var parsedUserId))
        {
            return parsedUserId;
        }

        throw new UnauthorizedAccessException("User ID not found in token");
    }

    private static ConnectionDto MapToConnectionDto(Connection connection)
    {
        return new ConnectionDto
        {
            ConnectionId = connection.ConnectionId,
            InitiatorUserId = connection.InitiatorUserId,
            RecipientUserId = connection.RecipientUserId,
            Status = connection.Status,
            ConnectionDate = connection.ConnectionDate,
            ResponseDate = connection.ResponseDate,
            Message = connection.Message
        };
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CompanyName = user.CompanyName,
            PhoneNumber = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            Bio = user.Bio,
            Skills = user.Skills,
            ServicePricing = user.ServicePricing,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }
}
