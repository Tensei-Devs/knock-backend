namespace Knock.Market.Api.Modules.Requests.Services;

internal sealed class RequestsApi : IRequestsApi
{
    public string ModuleName => RequestsModuleExtensions.ModuleKey;
}
