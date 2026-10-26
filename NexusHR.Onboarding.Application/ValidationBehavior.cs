using FluentValidation;
using MediatR;

namespace NexusHR.Onboarding.Application;

internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
            failures.AddRange((await validator.ValidateAsync(request, cancellationToken)).Errors);
        if (failures.Count > 0) throw new ValidationException(failures);
        return await next(cancellationToken);
    }
}
