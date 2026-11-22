using Knock.Identity.Api.Modules.Moderation.Data;
using Knock.Identity.Api.Modules.Moderation.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Identity.Api.Modules.Moderation;

/// <summary>
/// Módulo Moderation (spec 053): colas y decisiones del panel de revisión. Las dependencias de otras specs (fotos de la 025, recuperaciones de la 039, purga de la 057, suspensión de la 059 y
/// bandeja de salida de la 054) entran por interfaces con un valor por omisión que falla cerrado o no hace nada inofensivo; la spec dueña lo reemplaza. Requiere el módulo Panel (spec 033).
/// </summary>
public static class ModerationModuleExtensions
{
    public static IServiceCollection AddModerationModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<VerificationReviewStore>();
        services.AddSingleton<IVerificationReviewStore>(provider => provider.GetRequiredService<VerificationReviewStore>());
        services.AddSingleton<ReportReviewStore>();
        services.AddSingleton<IReportReviewStore>(provider => provider.GetRequiredService<ReportReviewStore>());
        services.TryAddSingleton<IVerificationPhotoLinks, UnavailableVerificationPhotoLinks>();
        services.TryAddSingleton<IVerificationBlobPurger, NoVerificationBlobs>();
        services.TryAddSingleton<IAccessRecoveryReviews, NoAccessRecoveryReviews>();
        services.TryAddSingleton<IReportDecisionEffects, ReportDecisionEffects>();
        services.AddSingleton<VerificationReviewService>();
        services.AddSingleton<ReportReviewService>();
        return services;
    }
}
