namespace Knock.Market.Api.Modules.Requests.Domain;

/// <summary>Quién es la persona frente a una visita (spec 049 y 050): decide si la operación sigue, es 403 por rol o 404 uniforme.</summary>
internal enum VisitRole
{
    Client,
    Technician,
    Stranger,
}

/// <summary><c>code</c> de las reglas de acceso a una visita, en inglés y en <c>snake_case</c>, sin datos de la persona.</summary>
internal static class VisitAccessCodes
{
    /// <summary>La persona participa en la visita pero la operación es de la otra parte (cliente dueño frente a técnico).</summary>
    public const string NotTheOwnerCode = "not_the_owner";
}

internal static class VisitAccess
{
    /// <summary>
    /// El papel de <paramref name="sub"/> en la visita. Quien no participa (<see cref="VisitRole.Stranger"/>) recibe 404 uniforme, para no revelar que el identificador existe;
    /// quien participa con el papel equivocado recibe 403.
    /// </summary>
    public static VisitRole RoleOf(string sub, string clientId, string technicianUserId)
    {
        if (string.Equals(sub, clientId, StringComparison.Ordinal))
        {
            return VisitRole.Client;
        }

        return string.Equals(sub, technicianUserId, StringComparison.Ordinal) ? VisitRole.Technician : VisitRole.Stranger;
    }
}
