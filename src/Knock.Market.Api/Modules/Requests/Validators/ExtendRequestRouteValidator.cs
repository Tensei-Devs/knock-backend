using FluentValidation;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Requests.Dtos;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Requests.Validators;

internal sealed class ExtendRequestRouteValidator : AbstractValidator<ExtendRequestRoute>
{
    public ExtendRequestRouteValidator() =>
        RuleFor(x => x.Id).Must(value => value is not null && ObjectId.TryParse(value, out _)).WithMessage(ValidationFilter.GenericReason);
}
