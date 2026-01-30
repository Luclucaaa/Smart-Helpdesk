using FluentValidation;
using SmartHelpdesk.Data.Enums;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Validators
{
    public class CreateTicketValidator : AbstractValidator<CreateTicketDTO>
    {
        public CreateTicketValidator() 
        {
            // Title là tùy chọn - nếu có thì max 100 ký tự
            RuleFor(ct => ct.Title)
                 .MaximumLength(100)
                 .When(ct => !string.IsNullOrEmpty(ct.Title));
            // Description là bắt buộc
            RuleFor(ct => ct.Description)
                 .NotNull()
                 .NotEmpty()
                 .WithMessage("Vui lòng mô tả vấn đề của bạn");
            RuleFor(ct => ct.Priority)
                 .IsInEnum();
            RuleFor(ct => ct.UserId)
                 .NotNull();
            // ProductName là tùy chọn - nếu có thì max 200 ký tự
            RuleFor(ct => ct.ProductName)
                 .MaximumLength(200)
                 .When(ct => !string.IsNullOrEmpty(ct.ProductName));
        }
    }
}
