using Fleet.Api.Requests;
using FluentValidation;

namespace Fleet.Api.Validation;

internal sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(request => request.VehicleId).NotEmpty();
        RuleFor(request => request.DriverId).NotEmpty();

        RuleFor(request => request.Purpose)
            .NotEmpty().WithMessage("A booking needs a purpose.")
            .MaximumLength(200);

        // Whether the window clashes with another booking is a domain rule enforced by the module
        // and by a Postgres exclusion constraint. That it ends after it starts needs neither.
        RuleFor(request => request.EndsAt)
            .GreaterThan(request => request.StartsAt)
            .WithMessage("A booking must end after it starts.");
    }
}

internal sealed class RescheduleBookingRequestValidator : AbstractValidator<RescheduleBookingRequest>
{
    public RescheduleBookingRequestValidator() =>
        RuleFor(request => request.EndsAt)
            .GreaterThan(request => request.StartsAt)
            .WithMessage("A booking must end after it starts.");
}
