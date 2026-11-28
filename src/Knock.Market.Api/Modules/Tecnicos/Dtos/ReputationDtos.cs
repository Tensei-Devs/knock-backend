namespace Knock.Market.Api.Modules.Tecnicos.Dtos;

/// <summary>
/// Reputación propia del técnico (spec 046, TEC-13, R-48): promedio con un decimal (<see langword="null"/> sin opiniones), total de opiniones, distribución por estrellas
/// (<c>"1"</c> a <c>"5"</c>) y nivel (<c>nuevo</c>, <c>confiable</c> o <c>destacado</c>, spec 051 R-14). Es el documento <c>reputacion</c> que calcula la spec 051.
/// </summary>
internal sealed record ReputationResponse(double? Promedio, int Total, IReadOnlyDictionary<string, int> Distribucion, string Nivel);
