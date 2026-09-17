using Fleet.Common.Results;
using FluentValidation;

namespace Fleet.Api.Workshop.Http;

/// <summary>
/// Runs FluentValidation over one argument of an endpoint, before the handler sees it.
/// </summary>
internal sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            // The endpoint does not take this type - wiring mistake
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        var details = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var error = Error.Validation(
            "request.invalid",
            "The request could not be accepted as sent.",
            details);

        return ProblemResults.From(error, context.HttpContext);
    }
}

internal static class ValidationFilterExtensions
{
    /// <summary>Validates the endpoint's <typeparamref name="TRequest"/> argument before it runs.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
