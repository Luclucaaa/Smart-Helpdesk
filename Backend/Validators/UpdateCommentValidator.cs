using FluentValidation;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Validators
{
    public class UpdateCommentValidator : AbstractValidator<UpdateCommentDTO>
    {
        public UpdateCommentValidator() 
        {
            RuleFor(uc => uc.Text)
                 .NotNull()
                 .NotEmpty();
        }
    }
}
