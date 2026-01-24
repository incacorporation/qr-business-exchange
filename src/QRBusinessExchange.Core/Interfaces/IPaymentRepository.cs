using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Repository interface for Payment entity operations
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Retrieves a payment by its unique identifier
    /// </summary>
    /// <param name="paymentId">The unique identifier of the payment</param>
    /// <returns>The payment if found, null otherwise</returns>
    Task<Payment?> GetByIdAsync(Guid paymentId);

    /// <summary>
    /// Retrieves all payments for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>A collection of payments for the user</returns>
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Creates a new payment in the repository
    /// </summary>
    /// <param name="payment">The payment entity to create</param>
    /// <returns>The created payment with generated ID</returns>
    Task<Payment> CreateAsync(Payment payment);

    /// <summary>
    /// Updates an existing payment in the repository
    /// </summary>
    /// <param name="payment">The payment entity to update</param>
    /// <returns>The updated payment</returns>
    Task<Payment> UpdateAsync(Payment payment);

    /// <summary>
    /// Retrieves a payment by its transaction ID
    /// </summary>
    /// <param name="transactionId">The transaction ID from the payment gateway</param>
    /// <returns>The payment if found, null otherwise</returns>
    Task<Payment?> GetByTransactionIdAsync(string transactionId);
}
