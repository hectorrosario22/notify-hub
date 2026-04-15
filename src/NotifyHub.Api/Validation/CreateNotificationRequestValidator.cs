using FluentValidation;
using NotifyHub.Contracts.Requests;
using NotifyHub.Core.Enums;

namespace NotifyHub.Api.Validation;

public sealed class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    private static readonly HashSet<string> ValidChannels = Enum.GetValues<Channel>()
        .Select(c => c.ToString().ToLowerInvariant())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("RecipientUserId cannot be empty.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Channels)
            .NotEmpty()
            .WithMessage("At least one channel must be specified.");

        RuleForEach(x => x.Channels)
            .Must(kv => ValidChannels.Contains(kv.Key))
            .WithMessage((_, kv) => $"'{kv.Key}' is not a valid channel. Valid channels: {string.Join(", ", ValidChannels)}.")
            .Must(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .WithMessage((_, kv) => $"Recipient for channel '{kv.Key}' cannot be empty.");
    }
}
