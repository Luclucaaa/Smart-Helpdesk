using FluentValidation;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Validators
{
    public class UpdateTicketValidator : AbstractValidator<UpdateTicketDTO>
    {
        public UpdateTicketValidator()
        {
            RuleFor(ut => ut.Title)
                 .NotNull()
                 .NotEmpty()
                 .Length(0, 100);
            RuleFor(ut => ut.Description)
                 .NotNull()
                 .NotEmpty();
            RuleFor(ut => ut.Priority)
                 .IsInEnum();
            RuleFor(ut => ut.Status)
                 .IsInEnum();
        }
    }
}
