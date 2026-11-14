using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// The signed-in user's contacts: people saved by scanning their business card.
/// </summary>
public class ContactService
{
    private readonly ApplicationDbContext _db;

    public ContactService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>All contacts of the user, newest first. Deactivated accounts are left out.</summary>
    public Task<List<ContactDto>> GetContactsAsync(Guid ownerUserId) =>
        _db.Contacts
            .AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId && c.ContactUser.IsActive)
            .OrderByDescending(c => c.AddedAt)
            .Select(c => new ContactDto
            {
                UserId = c.ContactUserId,
                FirstName = c.ContactUser.FirstName,
                LastName = c.ContactUser.LastName,
                // Only what the person chose to show on their business card.
                CompanyName = c.ContactUser.ShowCompanyName ? c.ContactUser.CompanyName : null,
                Email = c.ContactUser.ShowEmail ? c.ContactUser.Email : "",
                PhoneNumber = c.ContactUser.ShowPhoneNumber ? c.ContactUser.PhoneNumber : null,
                ProfileImageUrl = c.ContactUser.ShowProfileImage ? c.ContactUser.ProfileImageUrl : null,
                AddedAt = c.AddedAt
            })
            .ToListAsync();

    /// <summary>Whether the person is in the user's contacts.</summary>
    public Task<bool> IsContactAsync(Guid ownerUserId, Guid contactUserId) =>
        _db.Contacts.AnyAsync(c => c.OwnerUserId == ownerUserId && c.ContactUserId == contactUserId);

    /// <summary>
    /// Adds the person to the user's contacts. Adding someone who's already there is not an error:
    /// the existing entry is returned (scanning the same card twice is common).
    /// </summary>
    public async Task<ContactDto> AddContactAsync(Guid ownerUserId, Guid contactUserId)
    {
        if (ownerUserId == contactUserId)
        {
            throw new SsoException("own_card", "That's your own business card.");
        }

        var person = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == contactUserId && u.IsActive)
            ?? throw new SsoException("not_found", "This business card doesn't belong to a qrbound account.", 404);

        var existing = await _db.Contacts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId && c.ContactUserId == contactUserId);

        var addedAt = existing?.AddedAt ?? DateTime.UtcNow;
        if (existing == null)
        {
            _db.Contacts.Add(new Contact { OwnerUserId = ownerUserId, ContactUserId = contactUserId, AddedAt = addedAt });
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two scans at once: fine if the other request saved it first.
                if (!await IsContactAsync(ownerUserId, contactUserId))
                {
                    throw;
                }
            }
        }

        return new ContactDto
        {
            UserId = person.UserId,
            FirstName = person.FirstName,
            LastName = person.LastName,
            CompanyName = person.ShowCompanyName ? person.CompanyName : null,
            Email = person.ShowEmail ? person.Email : "",
            PhoneNumber = person.ShowPhoneNumber ? person.PhoneNumber : null,
            ProfileImageUrl = person.ShowProfileImage ? person.ProfileImageUrl : null,
            AddedAt = addedAt
        };
    }

    /// <summary>Removes the person from the user's contacts (nothing happens if they weren't there).</summary>
    public async Task RemoveContactAsync(Guid ownerUserId, Guid contactUserId)
    {
        await _db.Contacts
            .Where(c => c.OwnerUserId == ownerUserId && c.ContactUserId == contactUserId)
            .ExecuteDeleteAsync();
    }
}
