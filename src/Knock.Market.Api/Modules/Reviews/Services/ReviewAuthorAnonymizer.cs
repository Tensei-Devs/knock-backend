using Knock.Market.Api.Modules.Requests.Services;
using Knock.Market.Api.Modules.Reviews.Data;

namespace Knock.Market.Api.Modules.Reviews.Services;

/// <summary>Anonimiza las opiniones que escribió una cuenta eliminada (spec 039, R-25; spec 051, R-16 y CA-23): quedan marcadas <c>anonimizada</c>. La calificación sigue contando.</summary>
internal sealed class ReviewAuthorAnonymizer(IReviewStore store) : IOpinionAnonymizer
{
    public Task AnonymizeAsync(string sub, CancellationToken cancellationToken) => store.AnonymizeByAuthorAsync(sub, cancellationToken);
}
