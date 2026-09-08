using Fleet.Common.Results;
using FluentValidation;

namespace Fleet.Api.Http;

/// <summary>
/// Runs FluentValidation over one argument of an endpoint, before the handler sees it.
/// </summary>
/// <remarks>
/// <para>
/// An endpoint filter rather than middleware, because it needs to know the handler's arguments -
/// middleware sees only the raw request. Adding it to an endpoint is one call:
/// <c>.WithValidation&lt;RegisterVehicleRequest&gt;()</c>.
/// </para>
/// <para>
/// The failure is expressed as an <see cref="Error"/> with <see cref="ErrorKind.Validation"/> and
/// per-field details, so it goes through exactly the same problem-response path as a validation
/// failure raised inside a module. A client cannot tell which layer rejected it, and should not
/// need to.
/// </para>
/// </remarks>
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
            // The endpoint does not take this type, which is a wiring mistake rather than a
            // client one. Let it through; the handler will fail in a more informative way.
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid)
        {
            return await next(context);
        }

        var details = result.Errors
            .GroupBy(failure => failure.PropertyName)
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
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
