using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Crypto;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Domain;
using Knock.Identity.Api.Modules.Trust.Dtos;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>Aviso de privacidad del consentimiento biométrico (sección <c>Verificacion</c>): la versión vigente y su texto (R-09).</summary>
internal sealed class VerificationOptions
{
    public const string SectionName = "Verificacion";

    public string AvisoVersion { get; set; } = string.Empty;

    public string AvisoTexto { get; set; } = string.Empty;
}

/// <summary>
/// Crear, confirmar fotos, enviar y consultar la verificación propia (spec 025, T-05). La persona es siempre la del <c>sub</c>. SEGURIDAD Y CRIPTOGRAFÍA: la CURP solo se valida,
/// se convierte en HMAC y se descarta; nunca se guarda ni se registra.
/// </summary>
internal sealed class VerificationService(
    IVerificationStore store,
    IOptions<VerificationOptions> options,
    TimeProvider clock,
    CurpHmac curpHmac,
    CurpDuplicateGuard duplicates,
    CifradoDeVerificacion crypto,
    FotoVerificacionStore photos,
    IVerificationEvents events,
    VerificationPurgeScheduler purgeScheduler)
{
    /// <summary>La verificación vive 7 días en revisión (R-13).</summary>
    public static readonly TimeSpan ReviewLifetime = TimeSpan.FromDays(7);

    /// <summary>Una captura a medias vive 24 h (§14, punto 5).</summary>
    public static readonly TimeSpan CaptureLifetime = TimeSpan.FromHours(24);

    /// <summary>Crea la verificación. <see langword="null"/> si la cuenta o la sesión del token ya no existen (401).</summary>
    /// <exception cref="DomainException">422 o 409 con el código de la spec (§5).</exception>
    public async Task<VerificationCreatedResponse?> CreateAsync(string sub, string sessionId, CreateVerificationRequest request, CancellationToken cancellationToken)
    {
        var account = await store.FindAccountAsync(sub, cancellationToken);
        if (account is null)
        {
            return null;
        }

        EnsureNotScheduledForDeletion(account);

        // R-02 y R-09: sin valor por omisión, y el aviso aceptado debe ser el vigente.
        if (request.AceptaBiometria != true || !string.Equals(request.VersionAviso, options.Value.AvisoVersion, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorKind.Unprocessable, "Biometric consent is missing.", VerificationErrors.ConsentRequiredCode);
        }

        if (!Curp.IsValid(request.Curp))
        {
            throw new DomainException(DomainErrorKind.Unprocessable, "The CURP is not valid.", VerificationErrors.CurpInvalidCode);
        }

        EnsureCanStart(await store.FindLatestAsync(sub, cancellationToken), request.Nivel!);
        await EnsureNotDuplicateAsync(sub, request.Curp!, cancellationToken);
        var device = await store.FindSessionDeviceAsync(account.UserId, sessionId, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var curpHash = curpHmac.Compute(request.Curp!.Trim().ToUpperInvariant());
        var (dataKey, wrapped) = await crypto.CreateKeyAsync(cancellationToken);
        dataKey.Dispose();
        var verification = new NewVerification(ObjectId.GenerateNewId(), sub, request.Nivel!, now, now + CaptureLifetime, options.Value.AvisoTexto,
            options.Value.AvisoVersion, device, curpHash, wrapped);
        await store.CreateAsync(verification, cancellationToken);
        var links = await photos.IssueUploadLinksAsync(verification.Id, VerificationLevels.PhotosFor(verification.Level), cancellationToken);
        return new VerificationCreatedResponse(verification.Id.ToString(), VerificationStates.Capturing, verification.Level,
            [.. links.Select(link => new VerificationPhotoResponse(link.Type, PhotoStates.Pending, link.Url.AbsoluteUri, link.ExpiresAt))],
            verification.CaptureExpiresAt);
    }

    /// <summary>R-10 y R-12: confirma una foto subida a cuarentena. <see langword="null"/> si la cuenta ya no existe (401).</summary>
    /// <exception cref="DomainException">400 (id o tipo mal formados), 404 (ajena o inexistente), 409 y 422 con el código de la spec.</exception>
    public async Task<PhotoConfirmedResponse?> ConfirmPhotoAsync(string sub, string verificationId, string type, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(verificationId, out var id) || type is not (PhotoTypes.IdFront or PhotoTypes.IdBack or PhotoTypes.Selfie))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The verification id or photo type is malformed.");
        }

        var account = await store.FindAccountAsync(sub, cancellationToken);
        if (account is null)
        {
            return null;
        }

        EnsureNotScheduledForDeletion(account);
        var verification = await store.FindOwnedAsync(sub, id, cancellationToken) ?? throw NotFound();
        var photo = verification.Photos.FirstOrDefault(p => p.Type == type);
        if (photo is null)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The photo type does not belong to the level.", VerificationErrors.TypeInvalidCode);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (verification.State != VerificationStates.Capturing || verification.CaptureExpiresAt <= now)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The capture is not open.", VerificationErrors.CaptureExpiredCode);
        }

        if (photo.State == PhotoStates.Encrypted)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The photo was already confirmed.", VerificationErrors.PhotoAlreadyConfirmedCode);
        }

        var wrapped = verification.WrappedKey ?? throw new DomainException(DomainErrorKind.Conflict, "The capture is not open.", VerificationErrors.CaptureExpiredCode);
        await photos.ConfirmAsync(id, type, wrapped, cancellationToken);
        if (!await store.MarkPhotoEncryptedAsync(id, type, now, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.Conflict, "The photo was already confirmed.", VerificationErrors.PhotoAlreadyConfirmedCode);
        }

        return new PhotoConfirmedResponse(type, PhotoStates.Encrypted);
    }

    /// <summary>R-13: pasa a <c>en_revision</c> si todas las fotos del nivel están cifradas. <see langword="null"/> si la cuenta ya no existe (401).</summary>
    public async Task<VerificationSentResponse?> SubmitAsync(string sub, string verificationId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(verificationId, out var id))
        {
            throw new DomainException(DomainErrorKind.BadRequest, "The verification id is malformed.");
        }

        var account = await store.FindAccountAsync(sub, cancellationToken);
        if (account is null)
        {
            return null;
        }

        EnsureNotScheduledForDeletion(account);
        var verification = await store.FindOwnedAsync(sub, id, cancellationToken) ?? throw NotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        if (verification.State != VerificationStates.Capturing || verification.CaptureExpiresAt <= now)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The capture is not open.", VerificationErrors.CaptureExpiredCode);
        }

        if (verification.Photos.Count == 0 || verification.Photos.Any(p => p.State != PhotoStates.Encrypted))
        {
            throw new DomainException(DomainErrorKind.Conflict, "Some photos are missing.", VerificationErrors.PhotosMissingCode);
        }

        // R-06 al enviar: el HMAC ya guardado se compara con las demás cuentas; el índice único parcial cierra la carrera (CA-20).
        if (verification.CurpHmac is { } stored && await store.ExistsLiveCurpAsync([stored], sub, cancellationToken))
        {
            await events.PublishAsync("CurpDuplicada", sub, null, cancellationToken);
            throw DuplicateCurp();
        }

        var expires = now + ReviewLifetime;
        switch (await store.SubmitAsync(sub, id, now, expires, session => purgeScheduler.ScheduleAsync(session, id, now, cancellationToken), cancellationToken))
        {
            case SubmitOutcome.DuplicateCurp:
                await events.PublishAsync("CurpDuplicada", sub, null, cancellationToken);
                throw DuplicateCurp();
            case SubmitOutcome.NotSubmittable:
                throw new DomainException(DomainErrorKind.Conflict, "Some photos are missing.", VerificationErrors.PhotosMissingCode);
            default:
                await events.PublishAsync("VerificacionEnviada", sub, id.ToString(), cancellationToken);
                return new VerificationSentResponse(id.ToString(), VerificationStates.UnderReview, now, expires);
        }
    }

    private async Task EnsureNotDuplicateAsync(string sub, string curp, CancellationToken cancellationToken)
    {
        try
        {
            await duplicates.EnsureNotDuplicateAsync(sub, curp.Trim().ToUpperInvariant(), cancellationToken);
        }
        catch (DomainException ex) when (ex.Code == CommonErrorCodes.CurpDuplicate)
        {
            await events.PublishAsync("CurpDuplicada", sub, null, cancellationToken);
            throw;
        }
    }

    private static void EnsureNotScheduledForDeletion(VerificationAccount account)
    {
        if (account.State == "eliminacion_programada")
        {
            throw new DomainException(DomainErrorKind.Conflict, "The account has a scheduled deletion.", CommonErrorCodes.DeletionScheduled);
        }
    }

    private static DomainException NotFound() => new(DomainErrorKind.NotFound, "The verification was not found.");

    private static DomainException DuplicateCurp() => new(DomainErrorKind.Conflict, "This CURP cannot be used. Contact support.", CommonErrorCodes.CurpDuplicate);

    /// <summary>R-07: una en revisión bloquea; una verificada no se repite con el mismo nivel ni por debajo del otorgado.</summary>
    internal static void EnsureCanStart(StoredVerification? latest, string level)
    {
        if (latest is null)
        {
            return;
        }

        if (latest.State == VerificationStates.UnderReview)
        {
            throw new DomainException(DomainErrorKind.Conflict, "A verification is under review.", VerificationErrors.UnderReviewCode);
        }

        if (latest.State != VerificationStates.Verified)
        {
            return;
        }

        if (latest.GrantedLevel == VerificationLevels.Full)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The account is fully verified.", VerificationErrors.AlreadyFullyVerifiedCode);
        }

        if (level == VerificationLevels.Basic)
        {
            throw new DomainException(DomainErrorKind.Conflict, "The account is already verified at this level.", VerificationErrors.AlreadyVerifiedCode);
        }
    }

    /// <summary>R-14 y R-15: la más reciente, o <c>sin_verificar</c> si nunca hubo.</summary>
    public async Task<VerificationStatusResponse> GetAsync(string sub, CancellationToken cancellationToken)
    {
        var latest = await store.FindLatestAsync(sub, cancellationToken);
        if (latest is null)
        {
            return new VerificationStatusResponse(null, null, VerificationStates.NotVerified, null, [], null, [], null, null, null, null, PuedeReintentar: true);
        }

        var showsReason = latest.State is VerificationStates.Rejected or VerificationStates.NotVerified;
        var reason = showsReason ? latest.Reason : null;
        return new VerificationStatusResponse(
            latest.Id.ToString(),
            latest.Level,
            latest.State,
            reason,
            VerificationReasons.TipsFor(reason),
            latest.State == VerificationStates.Verified ? latest.GrantedLevel : null,
            [.. latest.Photos.Select(p => new VerificationPhotoStateResponse(p.Type, p.State))],
            latest.CreatedAt,
            latest.SentAt,
            latest.DecidedAt,
            latest.ExpiresAt,
            CanRetry(latest));
    }

    /// <summary>Puede empezar otra si no hay una en revisión y no tiene ya el nivel completo.</summary>
    internal static bool CanRetry(StoredVerification latest) =>
        latest.State != VerificationStates.UnderReview && !(latest.State == VerificationStates.Verified && latest.GrantedLevel == VerificationLevels.Full);
}
