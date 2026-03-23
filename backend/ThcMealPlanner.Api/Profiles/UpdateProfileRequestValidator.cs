using FluentValidation;

namespace ThcMealPlanner.Api.Profiles;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] AllowedRoles = ["head_of_household", "member", "dependent"];
    private static readonly string[] AllowedSeverity = ["mild", "moderate", "severe"];

    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name is not null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Role)
            .Must(role => role is null || AllowedRoles.Contains(role))
            .WithMessage("Role must be one of: head_of_household, member, dependent.");

        RuleForEach(x => x.DietaryPrefs)
            .NotEmpty()
            .MaximumLength(50);

        RuleForEach(x => x.ExcludedIngredients)
            .NotEmpty()
            .MaximumLength(100);

        RuleForEach(x => x.CuisinePreferences)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.DefaultServings)
            .InclusiveBetween(1, 20)
            .When(x => x.DefaultServings.HasValue);

        RuleForEach(x => x.DoctorNotes)
            .NotEmpty()
            .MaximumLength(400);

        RuleForEach(x => x.Allergies)
            .SetValidator(new AllergyModelValidator());

        RuleFor(x => x.MacroTargets)
            .SetValidator(new MacroTargetsModelValidator())
            .When(x => x.MacroTargets is not null);

        RuleFor(x => x.CookingConstraints)
            .SetValidator(new CookingConstraintsModelValidator())
            .When(x => x.CookingConstraints is not null);

        RuleFor(x => x.FlavorPreferences)
            .SetValidator(new FlavorPreferencesModelValidator())
            .When(x => x.FlavorPreferences is not null);

        RuleForEach(x => x.FamilyMembers)
            .SetValidator(new FamilyMemberModelValidator());

        RuleFor(x => x.NotificationPrefs)
            .SetValidator(new NotificationPreferencesModelValidator())
            .When(x => x.NotificationPrefs is not null);
    }

    private sealed class AllergyModelValidator : AbstractValidator<AllergyModel>
    {
        public AllergyModelValidator()
        {
            RuleFor(x => x.Allergen)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Severity)
                .NotEmpty()
                .Must(severity => AllowedSeverity.Contains(severity))
                .WithMessage("Severity must be one of: mild, moderate, severe.");

            RuleFor(x => x.Reaction)
                .MaximumLength(200)
                .When(x => x.Reaction is not null);
        }
    }

    private sealed class MacroTargetsModelValidator : AbstractValidator<MacroTargetsModel>
    {
        public MacroTargetsModelValidator()
        {
            RuleFor(x => x.Calories).GreaterThan(0).When(x => x.Calories.HasValue);
            RuleFor(x => x.Protein).GreaterThanOrEqualTo(0).When(x => x.Protein.HasValue);
            RuleFor(x => x.Carbohydrates).GreaterThanOrEqualTo(0).When(x => x.Carbohydrates.HasValue);
            RuleFor(x => x.Fat).GreaterThanOrEqualTo(0).When(x => x.Fat.HasValue);
            RuleFor(x => x.Fiber).GreaterThanOrEqualTo(0).When(x => x.Fiber.HasValue);
            RuleFor(x => x.Sodium).GreaterThanOrEqualTo(0).When(x => x.Sodium.HasValue);
        }
    }

    private sealed class CookingConstraintsModelValidator : AbstractValidator<CookingConstraintsModel>
    {
        public CookingConstraintsModelValidator()
        {
            RuleFor(x => x.MaxWeekdayPrepMinutes)
                .GreaterThanOrEqualTo(0)
                .When(x => x.MaxWeekdayPrepMinutes.HasValue);

            RuleFor(x => x.MaxWeekendPrepMinutes)
                .GreaterThanOrEqualTo(0)
                .When(x => x.MaxWeekendPrepMinutes.HasValue);

            RuleFor(x => x.BatchCookDay)
                .MaximumLength(20)
                .When(x => x.BatchCookDay is not null);
        }
    }

    private sealed class FlavorPreferencesModelValidator : AbstractValidator<FlavorPreferencesModel>
    {
        public FlavorPreferencesModelValidator()
        {
            RuleFor(x => x.SpiceLevel)
                .MaximumLength(20)
                .When(x => x.SpiceLevel is not null);

            RuleForEach(x => x.FavoriteHerbs)
                .NotEmpty()
                .MaximumLength(50);
        }
    }

    private sealed class FamilyMemberModelValidator : AbstractValidator<FamilyMemberModel>
    {
        public FamilyMemberModelValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Age)
                .InclusiveBetween(0, 120)
                .When(x => x.Age.HasValue);

            RuleFor(x => x.Preferences)
                .MaximumLength(200)
                .When(x => x.Preferences is not null);
        }
    }

    private sealed class NotificationPreferencesModelValidator : AbstractValidator<NotificationPreferencesModel>
    {
    }
}