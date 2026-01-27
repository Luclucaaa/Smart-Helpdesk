using FluentValidation;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Validators
{
    public class CreateCommentValidator : AbstractValidator<CreateCommentDTO>
    {
        public CreateCommentValidator()
        {
            RuleFor(cc => cc.Text)
                .NotNull()
                .NotEmpty();
            RuleFor(cc => cc.UserId)
                .NotNull();
            RuleFor(cc => cc.TicketId)
                .NotNull();
        }
    }
}
