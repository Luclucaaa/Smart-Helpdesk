using FluentValidation;
using System.Text.RegularExpressions;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Validators
{
    public class LoginValidator : AbstractValidator<UserLoginDTO>
    {
        public LoginValidator() 
        {
            RuleFor(ul => ul.Email)
                 .NotNull()
                 .EmailAddress();
            RuleFor(ut => ut.Password)
                 .NotNull()
                 .NotEmpty()
                 .Length(5, 15);
        }
    }
}
