using System.Text.Json.Serialization;

namespace Knock.Identity.Api.Modules.Safety.Dtos;

/// <summary>Cuerpo de <c>POST /suspension/apelacion</c> (spec 060, R-03): <c>texto</c> de 8 a 500 caracteres.</summary>
internal sealed class SuspensionAppealBody
{
    /// <summary>Límite del cuerpo: 2 KiB (R-07).</summary>
    public const int MaxBodyBytes = 2048;

    public string? Texto { get; set; }
}

/// <summary>La apelación tal como la ve la persona (spec 060, R-09): estado y fechas, nunca el texto de la revisión ni quién resolvió.</summary>
internal sealed record SuspensionAppealView(
    string Estado,
    DateTime PresentadaEn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? ResueltaEn);

/// <summary>Cuerpo de <c>POST /admin/apelaciones/{id}/resolucion</c> (spec 060, §5).</summary>
internal sealed class AppealResolutionBody
{
    public const int MaxBodyBytes = 1024;

    public string? Decision { get; set; }
}

/// <summary>Lo que responde la resolución: el reporte, su estado y la decisión tomada.</summary>
internal sealed record AppealResolutionResult(string Id, string Estado, string Decision);
