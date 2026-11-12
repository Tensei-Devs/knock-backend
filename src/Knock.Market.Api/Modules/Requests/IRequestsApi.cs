namespace Knock.Market.Api.Modules.Requests;

/// <summary>Contrato público del módulo Requests hacia el resto del servicio. Crece con las tareas de la spec 020; hoy solo identifica al módulo.</summary>
public interface IRequestsApi
{
    string ModuleName { get; }
}
