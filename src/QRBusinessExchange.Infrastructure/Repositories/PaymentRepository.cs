using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Payment entity operations
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentRepository"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    public PaymentRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<Payment?> GetByIdAsync(Guid paymentId)
    {
        try
        {
            return await _context.Payments
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving payment with ID {paymentId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.Payments
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving payments for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Payment> CreateAsync(Payment payment)
    {
        if (payment == null)
        {
            throw new ArgumentNullException(nameof(payment));
        }

        try
        {
            payment.CreatedAt = DateTime.UtcNow;

            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            return payment;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Error creating payment", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Payment> UpdateAsync(Payment payment)
    {
        if (payment == null)
        {
            throw new ArgumentNullException(nameof(payment));
        }

        try
        {
            var existingPayment = await _context.Payments.FindAsync(payment.PaymentId);
            if (existingPayment == null)
            {
                throw new InvalidOperationException($"Payment with ID {payment.PaymentId} not found");
            }

            _context.Entry(existingPayment).CurrentValues.SetValues(payment);
            await _context.SaveChangesAsync();

            return existingPayment;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error updating payment with ID {payment.PaymentId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Payment?> GetByTransactionIdAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));
        }

        try
        {
            return await _context.Payments
                .AsNoTracking()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving payment with transaction ID {transactionId}", ex);
        }
    }
}
