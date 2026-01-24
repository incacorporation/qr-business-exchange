using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Connection entity operations
/// </summary>
public class ConnectionRepository : IConnectionRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionRepository"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    public ConnectionRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<Connection?> GetByIdAsync(Guid connectionId)
    {
        try
        {
            return await _context.Connections
                .AsNoTracking()
                .Include(c => c.InitiatorUser)
                .Include(c => c.RecipientUser)
                .FirstOrDefaultAsync(c => c.ConnectionId == connectionId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving connection with ID {connectionId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Connection>> GetPendingByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.Connections
                .AsNoTracking()
                .Include(c => c.InitiatorUser)
                .Include(c => c.RecipientUser)
                .Where(c => c.RecipientUserId == userId && c.Status == "Pending")
                .OrderByDescending(c => c.ConnectionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving pending connections for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Connection>> GetHistoryAsync(Guid userId)
    {
        try
        {
            return await _context.Connections
                .AsNoTracking()
                .Include(c => c.InitiatorUser)
                .Include(c => c.RecipientUser)
                .Where(c => c.InitiatorUserId == userId || c.RecipientUserId == userId)
                .OrderByDescending(c => c.ConnectionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving connection history for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Connection> CreateAsync(Connection connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.InitiatorUserId == connection.RecipientUserId)
        {
            throw new InvalidOperationException("Users cannot connect with themselves");
        }

        try
        {
            connection.ConnectionDate = DateTime.UtcNow;

            await _context.Connections.AddAsync(connection);
            await _context.SaveChangesAsync();

            return connection;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Error creating connection", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Connection> UpdateStatusAsync(Guid connectionId, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        if (status != "Accepted" && status != "Rejected")
        {
            throw new ArgumentException("Status must be 'Accepted' or 'Rejected'", nameof(status));
        }

        try
        {
            var connection = await _context.Connections.FindAsync(connectionId);
            if (connection == null)
            {
                throw new InvalidOperationException($"Connection with ID {connectionId} not found");
            }

            connection.Status = status;
            connection.ResponseDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return connection;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error updating connection status for ID {connectionId}", ex);
        }
    }
}
