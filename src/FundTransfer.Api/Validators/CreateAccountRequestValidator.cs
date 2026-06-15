using FluentValidation;
using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;

namespace FundTransfer.Api.Validators;

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Owner).NotEmpty();
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Z]{3}$").WithMessage("Currency must be a 3-letter uppercase code.")
            .Must(c => DomainConstants.SupportedCurrencies.Contains(c)).WithMessage("Unsupported currency code.");
        RuleFor(x => x.InitialBalance).GreaterThanOrEqualTo(0L);
    }
}
