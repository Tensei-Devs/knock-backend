// jscpd:ignore-start -- EXC-015
namespace Knock.Identity.Api.Common.Errors;

/// <summary>Catálogo de errores de dominio. El 401 y el 429 no son excepciones de dominio: los producen la autenticación y el limitador.</summary>
public enum DomainErrorKind
{
    BadRequest,
    Forbidden,
    NotFound,
    Conflict,
    Unprocessable,
    ServiceUnavailable,
}

/// <summary>
/// Excepción base de dominio. El manejador la traduce a ProblemDetails con el código del catálogo.
/// El mensaje es solo para el log del servidor: nunca debe llevar datos del usuario y no se devuelve fuera de Development.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException()
        : this(DomainErrorKind.BadRequest, "Domain error.")
    {
    }

    public DomainException(string message)
        : this(DomainErrorKind.BadRequest, message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DomainException(DomainErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Error de dominio con un <c>code</c> estable que la app puede leer en el ProblemDetails (por ejemplo <c>account_suspended</c>).</summary>
    public DomainException(DomainErrorKind kind, string message, string code)
        : this(kind, message)
    {
        Code = code;
    }

    public DomainErrorKind Kind { get; }

    /// <summary>Código estable opcional; nunca lleva datos del usuario.</summary>
    public string? Code { get; }
}

/// <summary>Código HTTP de cada tipo del catálogo de dominio.</summary>
internal static class DomainErrors
{
    public static int ToStatusCode(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.BadRequest => StatusCodes.Status400BadRequest,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        DomainErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        DomainErrorKind.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown domain error kind."),
    };
}
// jscpd:ignore-end
