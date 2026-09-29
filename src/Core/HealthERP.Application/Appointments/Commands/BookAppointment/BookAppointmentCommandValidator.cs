using FluentValidation;

namespace HealthERP.Application.Appointments.Commands.BookAppointment;

public class BookAppointmentCommandValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("Patient selection is required.");

        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Doctor/Staff selection is required.");

        RuleFor(x => x.StartDateTimeUtc)
            .NotEmpty().WithMessage("Start time is required.")
            .LessThan(x => x.EndDateTimeUtc).WithMessage("Start time must be earlier than end time.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason for appointment is required.")
            .MaximumLength(500);
    }
}
