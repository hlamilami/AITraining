using FluentValidation;
using FundTransfer.Application.DTOs;

namespace FundTransfer.Api.Validators;

public class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        RuleFor(x => x.SourceAccountNumber).NotEmpty();
        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty()
            .NotEqual(x => x.SourceAccountNumber).WithMessage("Source and destination accounts must differ.");
        RuleFor(x => x.Amount).GreaterThan(0L);
    }
}
