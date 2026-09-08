using Fleet.Api.Requests;
using FluentValidation;

namespace Fleet.Api.Validation;

/// <summary>
/// Validates the shape of a request before it reaches a module.
/// </summary>
/// <remarks>
/// <para>
/// Note how little is here, and what is deliberately missing. There is no check that the plate is
/// unique, or that the depot exists: those are questions about the state of the system, only the
/// module can answer them, and it already does. Duplicating them here would mean two
/// implementations of one rule, drifting apart at their own pace.
/// </para>
/// <para>
/// What belongs here is everything answerable from the request alone - a required field, a
/// plausible range, a syntactically valid value. If you need the database to decide, it is not
/// validation, it is a domain rule.
/// </para>
/// </remarks>
internal sealed class RegisterVehicleRequestValidator : AbstractValidator<RegisterVehicleRequest>
{
    public RegisterVehicleRequestValidator()
    {
        RuleFor(request => request.Plate)
            .NotEmpty().WithMessage("A registration plate is required.")
            .MaximumLength(16);

        RuleFor(request => request.Type)
            .IsInEnum().WithMessage("Unknown vehicle type.");

        RuleFor(request => request.DepotId)
            .NotEmpty().WithMessage("A depot id is required.");

        RuleFor(request => request.OdometerKm)
            .GreaterThanOrEqualTo(0).WithMessage("Mileage cannot be negative.")
            .LessThanOrEqualTo(3_000_000).WithMessage("That mileage is not plausible.");
    }
}

internal sealed class ChangeVehicleStatusRequestValidator : AbstractValidator<ChangeVehicleStatusRequest>
{
    public ChangeVehicleStatusRequestValidator() =>
        RuleFor(request => request.Status).IsInEnum().WithMessage("Unknown vehicle status.");
}

internal sealed class RecordOdometerRequestValidator : AbstractValidator<RecordOdometerRequest>
{
    public RecordOdometerRequestValidator()
    {
        RuleFor(request => request.Km)
            .GreaterThanOrEqualTo(0).WithMessage("Mileage cannot be negative.");

        // That the reading is not *lower than the last one* is a domain rule and lives on the
        // Vehicle entity. This only rejects a reading from next year, which needs no database.
        RuleFor(request => request.RecordedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage("A reading cannot be taken in the future.");
    }
}
