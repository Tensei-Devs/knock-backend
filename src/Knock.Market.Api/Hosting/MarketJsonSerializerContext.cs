using System.Text.Json.Serialization;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Market.Api.Hosting;

/// <summary>Contexto de serialización JSON generado en compilación (spec 010, T-06): solo los DTO de respuesta de Market.</summary>
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(List<OficioResponse>))]
internal sealed partial class MarketJsonSerializerContext : JsonSerializerContext
{
}
