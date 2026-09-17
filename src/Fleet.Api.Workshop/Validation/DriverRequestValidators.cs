using Fleet.Api.Workshop.Requests;
using FluentValidation;

namespace Fleet.Api.Workshop.Validation;

/// <summary>
/// Validates the shape of a <see cref="RegisterDriverRequest"/> before it reaches the module.
/// </summary>
internal sealed class RegisterDriverRequestValidator : AbstractValidator<RegisterDriverRequest>
{
    public RegisterDriverRequestValidator()
    {
        RuleFor(request => request.EmployeeNumber).NotEmpty().MaximumLength(20);

        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
    }
}
