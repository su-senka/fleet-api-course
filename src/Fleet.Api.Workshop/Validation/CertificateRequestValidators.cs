using Fleet.Api.Workshop.Requests;
using FluentValidation;

namespace Fleet.Api.Workshop.Validation;

/// <summary>
/// Checks exactly what <c>Certificate.Issue</c> checks, so a malformed request comes back with
/// every field problem at once instead of one round trip per rule.
/// </summary>
internal sealed class AddCertificateRequestValidator : AbstractValidator<AddCertificateRequest>
{
    public AddCertificateRequestValidator()
    {
        RuleFor(request => request.Number).NotEmpty().MaximumLength(40);

        RuleFor(request => request.Kind).IsInEnum().WithMessage("Unknown certificate kind.");

        RuleFor(request => request.ExpiresOn)
            .GreaterThanOrEqualTo(request => request.IssuedOn)
            .WithMessage("A certificate cannot expire before it was issued.");
    }
}
