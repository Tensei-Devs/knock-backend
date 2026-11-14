using Knock.Market.Api.Hosting;
using Microsoft.AspNetCore.WebUtilities;

namespace Knock.Market.Api.Modules.Catalog.Controllers;

/// <summary>Maps search failures to sanitized ProblemDetails; used by the future search controller.</summary>
internal static class SearchProblems
{
    public static Task WriteAsync(HttpContext context, string error)
    {
        ArgumentNullException.ThrowIfNull(context);
        var status = error switch
        {
            "invalid_cursor" => StatusCodes.Status400BadRequest,
            "search_unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => throw new InvalidOperationException("Unknown search failure."),
        };
        return ProblemResponses.WriteAsync(context, status, ReasonPhrases.GetReasonPhrase(status), error);
    }
}
