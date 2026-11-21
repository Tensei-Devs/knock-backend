using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Moderation.Dtos;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Trust.Domain;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// Implementación real de <see cref="IVerificationPhotoLinks"/> del panel (spec 053, VER-04) sobre <see cref="FotoVerificacionStore"/> (spec 025, T-04). SEGURIDAD Y CRIPTOGRAFÍA: los enlaces
/// son SAS de lectura de un solo blob y 5 minutos; no se guardan, no se cachean y no se registran. LÍMITE CONOCIDO: el blob definitivo está cifrado con la llave de la verificación, así que el
/// enlace entrega el envoltorio cifrado; mostrar la foto exige un paso de descifrado en el servidor (<see cref="FotoVerificacionStore.ReadDecryptedAsync"/>), decisión pendiente de validar.
/// </summary>
internal sealed class TrustVerificationPhotoLinks(FotoVerificacionStore photos) : IVerificationPhotoLinks
{
    public async Task<IReadOnlyList<VerificationPhotoLink>> IssueAsync(string verificationId, IReadOnlyList<string> photoTypes, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(verificationId, out var id))
        {
            throw new DomainException(DomainErrorKind.NotFound, "The verification was not found.");
        }

        var issued = await photos.IssueReadLinksAsync(id, photoTypes, lifetime, cancellationToken);
        return [.. issued.Select(link => new VerificationPhotoLink(link.Type, link.Url.AbsoluteUri, link.ExpiresAt))];
    }
}

/// <summary>Implementación real de <see cref="IVerificationBlobPurger"/> (spec 057): borra cuarentena y definitivo de los tres tipos de foto; idempotente.</summary>
internal sealed class TrustVerificationBlobPurger(FotoVerificacionStore photos) : IVerificationBlobPurger
{
    private static readonly string[] _allTypes = [PhotoTypes.IdFront, PhotoTypes.IdBack, PhotoTypes.Selfie];

    public Task PurgeBlobsAsync(string verificationId, CancellationToken cancellationToken) =>
        ObjectId.TryParse(verificationId, out var id) ? photos.PurgeAsync(id, _allTypes, cancellationToken) : Task.CompletedTask;
}
