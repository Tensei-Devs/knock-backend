using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Sesiones.Data;
using Knock.Identity.Api.Modules.Sesiones.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Sesiones.Services;

/// <summary>Datos de un canje que no vienen del token: lo que manda la app.</summary>
internal sealed record LoginRequest(string FirebaseIdToken, string DeviceId, string DeviceName);

/// <summary>Resultado de un canje: la sesión y si la cuenta se creó en este canje (201) o ya existía (200).</summary>
internal sealed record LoginResult(IssuedSession Session, bool IsNew, bool ProfileComplete);

/// <summary>Versiones vigentes del aviso de privacidad y de los términos (spec 015, §9): constante <c>1.0</c> en configuración hasta que el texto legal esté vigente.</summary>
internal sealed class ConsentVersionsOptions
{
    public const string SectionName = "Consentimientos";

    public string AvisoPrivacidad { get; set; } = string.Empty;

    public string Terminos { get; set; } = string.Empty;
}

/// <summary>
/// Canjea un ID token de Firebase por la sesión propia (spec 015): deriva el <c>sub</c>, busca la cuenta por <c>sub</c> y por el HMAC del celular,
/// crea la cuenta completa en una transacción si no existe y abre la sesión del dispositivo guardando solo la huella del token de renovación. Ni el
/// celular ni el <c>uid</c> ni los tokens llegan a los registros.
/// </summary>
internal sealed partial class LoginService(
    IFirebaseIdTokenVerifier verifier,
    SessionTokenIssuer issuer,
    PhoneProtector phones,
    ISesionesStore store,
    IOptions<ConsentVersionsOptions> consents,
    TimeProvider timeProvider,
    ILogger<LoginService> logger)
{
    public const string AccountSuspendedCode = "account_suspended";
    public const string AccountConflictCode = "account_conflict";

    /// <returns>El resultado, o <c>null</c> si el token se rechaza (inválido, vencido, sin teléfono +52 o de otro proyecto).</returns>
    /// <exception cref="DomainException">403 <c>account_suspended</c> o 409 <c>account_conflict</c>.</exception>
    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var identity = await verifier.VerifyAsync(request.FirebaseIdToken, cancellationToken);
        if (identity is null)
        {
            LogRejected(logger);
            return null;
        }

        var sub = UserIdDerivation.Derive(identity.Uid);
        var phoneHash = phones.Hash(identity.PhoneNumber);
        var bySub = await store.FindBySubAsync(sub.ToString("D"), cancellationToken);
        var byPhone = await store.FindByPhoneHashAsync(phoneHash.Value, cancellationToken);
        if (bySub is null != byPhone is null)
        {
            // Las dos lecturas no son atómicas: si un canje simultáneo confirmó la cuenta entre ellas, una vio la cuenta y la otra no.
            // Se leen otra vez antes de declarar conflicto (R-05, R-06).
            bySub = await store.FindBySubAsync(sub.ToString("D"), cancellationToken);
            byPhone = await store.FindByPhoneHashAsync(phoneHash.Value, cancellationToken);
        }

        if (bySub is null && byPhone is null)
        {
            return await CreateAsync(request, identity, sub, phoneHash, cancellationToken);
        }

        if (bySub is null || byPhone is null || bySub.Id != byPhone.Id)
        {
            throw Conflict();
        }

        return await ExistingAsync(request, bySub, sub, cancellationToken);
    }

    private async Task<LoginResult> ExistingAsync(LoginRequest request, UserAccount account, Guid sub, CancellationToken cancellationToken)
    {
        if (account.State == AccountState.Suspended)
        {
            LogSuspended(logger);
            throw new DomainException(DomainErrorKind.Forbidden, "The account is suspended.", AccountSuspendedCode);
        }

        var (session, issued) = OpenSession(request, account.Id, sub);
        await store.AddSessionAsync(session, cancellationToken);
        LogIssued(logger);
        return new LoginResult(issued, IsNew: false, account.ProfileComplete);
    }

    private async Task<LoginResult> CreateAsync(
        LoginRequest request, FirebaseIdentity identity, Guid sub, PhoneHash phoneHash, CancellationToken cancellationToken)
    {
        var userId = ObjectId.GenerateNewId().ToString();
        var (session, issued) = OpenSession(request, userId, sub);
        var protectedPhone = phones.Protect(identity.PhoneNumber, userId);
        var account = new NewAccount(
            userId,
            sub.ToString("D"),
            protectedPhone.Ciphertext,
            protectedPhone.KeyVersion,
            phoneHash.Value,
            phoneHash.KeyVersion,
            consents.Value.AvisoPrivacidad,
            consents.Value.Terminos,
            timeProvider.GetUtcNow().UtcDateTime,
            session);

        if (await store.CreateAccountAsync(account, cancellationToken) == AccountCreation.Created)
        {
            LogCreated(logger);
            return new LoginResult(issued, IsNew: true, ProfileComplete: false);
        }

        // Otro canje ganó la carrera (R-05): no se guardó nada de este; se vuelve a leer y se trata como cuenta existente (o como conflicto).
        var winner = await store.FindBySubAsync(account.Sub, cancellationToken);
        var winnerByPhone = await store.FindByPhoneHashAsync(phoneHash.Value, cancellationToken);
        if (winner is null || winnerByPhone is null || winner.Id != winnerByPhone.Id)
        {
            throw Conflict();
        }

        return await ExistingAsync(request, winner, sub, cancellationToken);
    }

    // Cada canje abre la sesión de un dispositivo: el id de la sesión es el `sid` del JWT y de ahí sale la huella del token de renovación.
    private (NewSession Session, IssuedSession Issued) OpenSession(LoginRequest request, string userId, Guid sub)
    {
        var sessionId = ObjectId.GenerateNewId().ToString();
        var issued = issuer.Issue(sub, sessionId);
        var session = new NewSession(
            sessionId,
            userId,
            sub.ToString("D"),
            request.DeviceId,
            request.DeviceName,
            RefreshTokenFingerprint.Compute(issued.RefreshToken),
            timeProvider.GetUtcNow().UtcDateTime);
        return (session, issued);
    }

    private DomainException Conflict()
    {
        LogConflict(logger);
        return new DomainException(DomainErrorKind.Conflict, "The phone or the identity is already linked to another account.", AccountConflictCode);
    }

    // Los registros no llevan ID token, teléfono, uid, sub ni tokens de la sesión.
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Firebase session exchange rejected")]
    private static partial void LogRejected(ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Firebase session exchange succeeded")]
    private static partial void LogIssued(ILogger logger);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Account created by Firebase session exchange")]
    private static partial void LogCreated(ILogger logger);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Firebase session exchange refused: account suspended")]
    private static partial void LogSuspended(ILogger logger);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Firebase session exchange refused: phone or identity linked to another account")]
    private static partial void LogConflict(ILogger logger);
}
