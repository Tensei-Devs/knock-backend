using System.Globalization;
using System.Text;
using FluentValidation;
using Knock.Market.Api.Modules.Reviews.Domain;
using Knock.Market.Api.Modules.Reviews.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Reviews.Validators;

/// <summary>Reglas de forma compartidas de las rutas de la spec 051 (R-02, R-07, R-10): nada se repite en el error.</summary>
internal static class ReviewFormat
{
    public const string Invalid = "The value is not valid.";

    public static bool ValidObjectId(string? value) => value is { Length: 24 } && ObjectId.TryParse(value, out _);

    /// <summary>Una foto de la opinión es el identificador (<c>Guid</c>) de un archivo del módulo de archivos.</summary>
    public static bool IsPhotoId(string? value) => Guid.TryParseExact(value, "D", out _);

    public static bool ValidParts(ReviewParts? parts) =>
        parts is null || new[] { parts.Puntualidad, parts.Calidad, parts.PrecioJusto, parts.Trato }.All(v => v is null || ReviewRules.IsValidStars(v));

    /// <summary>Cursor opaco: fecha en ticks y <c>ObjectId</c> de la última opinión de la página.</summary>
    public static string EncodeCursor(DateTime createdAtUtc, ObjectId id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{createdAtUtc.Ticks}|{id}")));

    public static bool TryDecodeCursor(string? value, out DateTime createdAtUtc, out ObjectId id)
    {
        createdAtUtc = default;
        id = default;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks > DateTime.MaxValue.Ticks)
            {
                return false;
            }

            createdAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            return ObjectId.TryParse(parts[1], out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>OPI-01 (R-02 y R-04): <c>id</c> es un <c>ObjectId</c>, <c>limite</c> de 1 a 50 y el cursor, uno emitido por esta ruta.</summary>
internal sealed class TechnicianReviewsQueryValidator : AbstractValidator<TechnicianReviewsQuery>
{
    public TechnicianReviewsQueryValidator()
    {
        RuleFor(x => x.Id).Must(ReviewFormat.ValidObjectId).WithMessage(ReviewFormat.Invalid);
        RuleFor(x => x.Limite).InclusiveBetween(1, ReviewRules.MaxLimit).WithMessage(ReviewFormat.Invalid);
        RuleFor(x => x.Cursor).Must(c => ReviewFormat.TryDecodeCursor(c, out _, out _)).WithMessage(ReviewFormat.Invalid).When(x => x.Cursor is not null);
    }
}

internal sealed class ReviewVisitRouteValidator : AbstractValidator<ReviewVisitRoute>
{
    public ReviewVisitRouteValidator() => RuleFor(x => x.Id).Must(ReviewFormat.ValidObjectId).WithMessage(ReviewFormat.Invalid);
}

internal sealed class ReviewRouteValidator : AbstractValidator<ReviewRoute>
{
    public ReviewRouteValidator() => RuleFor(x => x.Id).Must(ReviewFormat.ValidObjectId).WithMessage(ReviewFormat.Invalid);
}

/// <summary>OPI-02 (R-07 y R-13): calificación de 1 a 5, partes de 1 a 5, comentario de hasta 500 caracteres y hasta 3 fotos.</summary>
internal sealed class CreateReviewBodyValidator : AbstractValidator<CreateReviewBody>
{
    public CreateReviewBodyValidator()
    {
        RuleFor(x => x.Calificacion).NotNull().Must(ReviewRules.IsValidStars).WithMessage(ReviewFormat.Invalid);
        RuleFor(x => x.Partes).Must(ReviewFormat.ValidParts).WithMessage(ReviewFormat.Invalid);
        RuleFor(x => x.Comentario).Must(ReviewRules.IsValidText).WithMessage(ReviewFormat.Invalid);
        RuleFor(x => x.Fotos).Must(p => p is null || (p.Count <= ReviewRules.MaxPhotos && p.All(ReviewFormat.IsPhotoId))).WithMessage(ReviewFormat.Invalid);
    }
}

/// <summary>OPI-03 (R-10 y R-15): <c>texto</c> obligatorio de 1 a 500 caracteres.</summary>
internal sealed class AnswerReviewBodyValidator : AbstractValidator<AnswerReviewBody>
{
    public AnswerReviewBodyValidator() => RuleFor(x => x.Texto).Must(ReviewRules.IsValidAnswer).WithMessage(ReviewFormat.Invalid);
}
