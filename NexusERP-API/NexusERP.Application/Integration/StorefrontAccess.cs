using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Application.Integration;

/// <summary>
/// The Khazar storefront is one shared system, so nothing may be sent to it for a company that is not connected to
/// it. A company is connected when it has a storefront name (<see cref="Domain.Entities.Company.StorefrontName"/>).
/// </summary>
public static class StorefrontAccess
{
    public static Task<bool> IsEnabledAsync(IApplicationDbContext db, string companyId, CancellationToken cancellationToken) =>
        db.Companies.AsNoTracking().AnyAsync(
            c => c.Id == companyId && c.StorefrontName != null && c.StorefrontName != "", cancellationToken);

    public const string NotConnectedMessage = "Şirkətiniz e-ticarət vitrininə qoşulmayıb.";
}
