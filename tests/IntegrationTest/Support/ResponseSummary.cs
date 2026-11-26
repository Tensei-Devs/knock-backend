using System.Net;

namespace Knock.IntegrationTest.Support;

/// <summary>Resumen de una ráfaga de respuestas para el mensaje de una aserción: cuántas por código y el motivo de las que no son éxito.</summary>
internal sealed record ResponseSummary(IReadOnlyList<HttpStatusCode> Statuses, string Detail)
{
    public int Count(HttpStatusCode status) => Statuses.Count(s => s == status);

    public override string ToString() =>
        string.Join(", ", Statuses.GroupBy(s => s).OrderBy(g => (int)g.Key).Select(g => $"{(int)g.Key}x{g.Count()}")) + (Detail.Length == 0 ? string.Empty : $"; {Detail}");

    /// <summary>Lee y descarta las respuestas: guarda los códigos y, de las de error, el texto de motivos (sin traceId ni instance).</summary>
    public static async Task<ResponseSummary> ReadAsync(IEnumerable<HttpResponseMessage> responses)
    {
        ArgumentNullException.ThrowIfNull(responses);
        var statuses = new List<HttpStatusCode>();
        var reasons = new List<string>();
        foreach (var response in responses)
        {
            using (response)
            {
                statuses.Add(response.StatusCode);
                if ((int)response.StatusCode >= 400 && response.StatusCode != HttpStatusCode.Conflict)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    if (reasons.Count < 3)
                    {
                        reasons.Add($"{(int)response.StatusCode}: {Trim(ProblemBody.Reasons(text))}");
                    }
                }
            }
        }

        return new ResponseSummary(statuses, string.Join(" | ", reasons));
    }

    private static string Trim(string text) => text.Length <= 300 ? text : text[..300] + "...";
}
