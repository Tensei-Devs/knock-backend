using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Knock.Hosting.Mvc;

/// <summary>
/// <c>Cache-Control: private, no-store</c> en toda respuesta del controller (specs 020 R-18 y 030 R-20): los datos son personales y cambian al escribir, y no llevan <c>ETag</c>.
/// Se fija al empezar la acción.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PrivateNoStoreAttribute : ActionFilterAttribute
{
    public const string Value = "private, no-store";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Response.Headers[HeaderNames.CacheControl] = Value;
        base.OnActionExecuting(context);
    }
}
