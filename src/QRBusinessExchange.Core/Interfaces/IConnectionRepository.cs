using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Repository interface for Connection entity operations
/// </summary>
public interface IConnectionRepository
{
    /// <summary>
    /// Retrieves a connection by its unique identifier
    /// </summary>
    /// <param name="connectionId">The unique identifier of the connection</param>
    /// <returns>The connection if found, null otherwise</returns>
    Task<Connection?> GetByIdAsync(Guid connectionId);

    /// <summary>
    /// Retrieves all pending connection requests for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>A collection of pending connections</returns>
    Task<IEnumerable<Connection>> GetPendingByUserIdAsync(Guid userId);

    /// <summary>
    /// Retrieves the connection history for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>A collection of all connections (sent and received)</returns>
    Task<IEnumerable<Connection>> GetHistoryAsync(Guid userId);

    /// <summary>
    /// Creates a new connection request in the repository
    /// </summary>
    /// <param name="connection">The connection entity to create</param>
    /// <returns>The created connection with generated ID</returns>
    Task<Connection> CreateAsync(Connection connection);

    /// <summary>
    /// Updates the status of a connection request
    /// </summary>
    /// <param name="connectionId">The unique identifier of the connection</param>
    /// <param name="status">The new status (Accepted/Rejected)</param>
    /// <returns>The updated connection</returns>
    Task<Connection> UpdateStatusAsync(Guid connectionId, string status);
}
