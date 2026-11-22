using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Domain;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// Detección de CURP duplicada al enviar a revisión (spec 025, R-06). Busca con el HMAC de <b>todas</b> las versiones de llave (<see cref="CurpHmac.ComputeAll"/>), no
/// solo la vigente: después de una rotación, una CURP guardada con la llave anterior sigue contando como duplicada. El mensaje es genérico y no revela nada de la otra cuenta.
/// </summary>
internal sealed class CurpDuplicateGuard(CurpHmac curpHmac, IVerificationStore store)
{
    /// <exception cref="DomainException">409 <c>curp_duplicate</c> si otra cuenta tiene esa CURP en revisión o verificada.</exception>
    public async Task EnsureNotDuplicateAsync(string sub, string curp, CancellationToken cancellationToken)
    {
        if (await store.ExistsLiveCurpAsync(curpHmac.ComputeAll(curp), sub, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "This CURP cannot be used. Contact support.", CommonErrorCodes.CurpDuplicate);
        }
    }
}
