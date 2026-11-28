namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>Cuerpo de <c>POST /api/v1/interno/tecnicos-ocultados</c> (spec 053, R-20): el evento <c>TecnicoOcultado</c> con solo identificadores, sin texto del reporte ni datos de la persona.</summary>
internal sealed class TechnicianHiddenEventBody
{
    public string? EventoId { get; set; }

    public string? UsuarioId { get; set; }

    public string? ReporteId { get; set; }
}
