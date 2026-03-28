using FluentValidation;

namespace ThcMealPlanner.Api.Chat;

public sealed class ChatMessageRequestValidator : AbstractValidator<ChatMessageRequest>
{
    public ChatMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.ConversationId)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.ConversationId));
    }
}
