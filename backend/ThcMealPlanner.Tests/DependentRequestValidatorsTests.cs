using FluentAssertions;
using ThcMealPlanner.Api.Profiles;

namespace ThcMealPlanner.Tests;

public sealed class DependentRequestValidatorsTests
{
    [Fact]
    public void CreateValidator_WithValidRequest_ReturnsValid()
    {
        var validator = new CreateDependentRequestValidator();
        var request = new CreateDependentRequest
        {
            Name = "Child 1",
            AgeGroup = "elementary",
            DietaryPrefs = ["vegetarian"],
            Allergies =
            [
                new AllergyModel
                {
                    Allergen = "peanuts",
                    Severity = "severe"
                }
            ],
            MacroTargets = new MacroTargetsModel
            {
                Calories = 1200
            }
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidator_WithInvalidNestedFields_ReturnsExpectedErrors()
    {
        var validator = new CreateDependentRequestValidator();
        var request = new CreateDependentRequest
        {
            Name = "",
            Allergies =
            [
                new AllergyModel
                {
                    Allergen = "",
                    Severity = ""
                }
            ],
            MacroTargets = new MacroTargetsModel
            {
                Calories = -1
            }
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Name");
        result.Errors.Should().Contain(error => error.PropertyName == "Allergies[0].Allergen");
        result.Errors.Should().Contain(error => error.PropertyName == "Allergies[0].Severity");
        result.Errors.Should().Contain(error => error.PropertyName == "MacroTargets.Calories");
    }

    [Fact]
    public void UpdateValidator_WithInvalidLengths_ReturnsExpectedErrors()
    {
        var validator = new UpdateDependentRequestValidator();
        var request = new UpdateDependentRequest
        {
            Name = new string('x', 101),
            Notes = new string('n', 501),
            PreferredFoods = [""]
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Name");
        result.Errors.Should().Contain(error => error.PropertyName == "Notes");
        result.Errors.Should().Contain(error => error.PropertyName == "PreferredFoods[0]");
    }
}