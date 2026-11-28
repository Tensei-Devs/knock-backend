using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Knock.ContractTest.Support;

/// <summary>Lectura de operaciones («VERBO /ruta») del contrato publicado y de las rutas reales de un host (spec 034, R-16).</summary>
internal static partial class ContractReader
{
    private static readonly HashSet<string> _httpMethods = new(["GET", "PUT", "POST", "DELETE", "PATCH"], StringComparer.Ordinal);

    /// <summary>Operaciones del contrato, por ejemplo <c>GET /api/v{version}/busqueda</c>.</summary>
    public static SortedSet<string> ContractOperations(JsonElement root)
    {
        var operations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject().Where(item => _httpMethods.Contains(item.Name.ToUpperInvariant())))
            {
                operations.Add($"{method.Name.ToUpperInvariant()} {path.Name}");
            }
        }

        return operations;
    }

    /// <summary>Acciones de controller que el host atiende, con la misma forma que el contrato.</summary>
    public static SortedSet<string> HostOperations(IEnumerable<EndpointDataSource> sources)
    {
        var operations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in sources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            {
                continue;
            }

            var path = Normalize(endpoint.RoutePattern.RawText ?? string.Empty);
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [];
            foreach (var method in methods.Where(method => _httpMethods.Contains(method.ToUpperInvariant())))
            {
                operations.Add($"{method.ToUpperInvariant()} {path}");
            }
        }

        return operations;
    }

    /// <summary>Quita restricciones y opcionales de los parámetros y antepone la barra: <c>api/v{version:apiVersion}/x/{id:guid?}</c> pasa a <c>/api/v{version}/x/{id}</c>.</summary>
    public static string Normalize(string pattern)
    {
        var simple = ConstraintRegex().Replace(pattern, "{$1}");
        return "/" + simple.TrimStart('/');
    }

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)(?::[^}]*)?\??\}")]
    private static partial Regex ConstraintRegex();
}
