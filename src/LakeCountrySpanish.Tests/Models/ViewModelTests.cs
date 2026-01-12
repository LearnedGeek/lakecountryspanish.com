using System.ComponentModel.DataAnnotations;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Tests.Models;

public class ViewModelTests
{
    [Fact]
    public void SubmitFeedbackViewModel_Rating_RequiresValidRange()
    {
        // Arrange
        var model = new SubmitFeedbackViewModel
        {
            ScheduledClassId = 1,
            Rating = 0 // Invalid - should be 1-5
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Rating"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void SubmitFeedbackViewModel_Rating_AcceptsValidValues(int rating)
    {
        // Arrange
        var model = new SubmitFeedbackViewModel
        {
            ScheduledClassId = 1,
            Rating = rating
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("Rating"));
    }

    [Fact]
    public void SendTipViewModel_Amount_RequiresMinimumValue()
    {
        // Arrange
        var model = new SendTipViewModel
        {
            Amount = 0 // Invalid - should be at least 1
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void SendTipViewModel_Amount_RequiresMaximumValue()
    {
        // Arrange
        var model = new SendTipViewModel
        {
            Amount = 1001 // Invalid - should be max 1000
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("Amount"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(1000)]
    public void SendTipViewModel_Amount_AcceptsValidValues(decimal amount)
    {
        // Arrange
        var model = new SendTipViewModel
        {
            Amount = amount
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void StudentProfileViewModel_HasCustomRate_ReturnsCorrectly()
    {
        // Arrange
        var modelWithCustomRate = new StudentProfileViewModel
        {
            CustomHourlyRate = 20.00m,
            StandardRate = 25.00m
        };
        var modelWithoutCustomRate = new StudentProfileViewModel
        {
            CustomHourlyRate = null,
            StandardRate = 25.00m
        };

        // Assert
        Assert.True(modelWithCustomRate.HasCustomRate);
        Assert.False(modelWithoutCustomRate.HasCustomRate);
    }

    [Fact]
    public void PurchaseClassesViewModel_HasCustomRate_ReturnsCorrectly()
    {
        // Arrange
        var modelWithCustomRate = new PurchaseClassesViewModel
        {
            CustomHourlyRate = 20.00m,
            BaseClassPrice = 25.00m
        };
        var modelWithoutCustomRate = new PurchaseClassesViewModel
        {
            CustomHourlyRate = null,
            BaseClassPrice = 25.00m
        };

        // Assert
        Assert.True(modelWithCustomRate.HasCustomRate);
        Assert.False(modelWithoutCustomRate.HasCustomRate);
    }

    [Fact]
    public void ChangePasswordViewModel_ConfirmPassword_MustMatch()
    {
        // Arrange
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "DifferentPass123!" // Doesn't match
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Contains(validationResults, v => v.MemberNames.Contains("ConfirmPassword"));
    }

    [Fact]
    public void ChangePasswordViewModel_MatchingPasswords_PassValidation()
    {
        // Arrange
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!" // Matches
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.DoesNotContain(validationResults, v => v.MemberNames.Contains("ConfirmPassword"));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}
