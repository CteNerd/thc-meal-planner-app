using FluentAssertions;
using ThcMealPlanner.Api.Profiles;

namespace ThcMealPlanner.Tests;

public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidPayload_ReturnsValid()
    {
        var request = new UpdateProfileRequest
        {
            Name = "Adult 1",
            Email = "adult1@example.com",
            DefaultServings = 4,
            Allergies =
            [
                new AllergyModel
                {
                    Allergen = "tree nuts",
                    Severity = "severe",
                    Reaction = "rash"
                }
            ],
            FamilyMembers =
            [
                new FamilyMemberModel
                {
                    Name = "Child 1",
                    Age = 8
                }
            ]
        };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenRoleProvided_ReturnsRoleError()
    {
        var result = _validator.Validate(new UpdateProfileRequest { Role = "member" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == "Role" &&
            error.ErrorMessage.Contains("Role cannot be updated", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenSeverityInvalid_ReturnsNestedAllergyError()
    {
        var result = _validator.Validate(
            new UpdateProfileRequest
            {
                Allergies =
                [
                    new AllergyModel
                    {
                        Allergen = "milk",
                        Severity = "critical"
                    }
                ]
            });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == "Allergies[0].Severity" &&
            error.ErrorMessage.Contains("Severity must be one of", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenFamilyMemberAgeOutOfRange_ReturnsFamilyMemberError()
    {
        var result = _validator.Validate(
            new UpdateProfileRequest
            {
                FamilyMembers =
                [
                    new FamilyMemberModel
                    {
                        Name = "Older Person",
                        Age = 130
                    }
                ]
            });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == "FamilyMembers[0].Age" &&
            error.ErrorMessage.Contains("between 0 and 120", StringComparison.OrdinalIgnoreCase));
    }
}