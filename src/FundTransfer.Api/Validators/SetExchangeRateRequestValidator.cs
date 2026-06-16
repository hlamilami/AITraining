using FluentValidation;
using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;

namespace FundTransfer.Api.Validators;

public class SetExchangeRateRequestValidator : AbstractValidator<SetExchangeRateRequest>
{
    public SetExchangeRateRequestValidator()
    {
        RuleFor(x => x.SourceCurrency).NotEmpty().Matches("^[A-Z]{3}$")
            .Must(c => DomainConstants.SupportedCurrencies.Contains(c)).WithMessage("Unsupported source currency.");
        RuleFor(x => x.TargetCurrency).NotEmpty().Matches("^[A-Z]{3}$")
            .Must(c => DomainConstants.SupportedCurrencies.Contains(c)).WithMessage("Unsupported target currency.")
            .NotEqual(x => x.SourceCurrency).WithMessage("Source and target currencies must differ.");
        RuleFor(x => x.Rate).GreaterThan(0).WithMessage("Rate must be greater than zero.");
    }
}
