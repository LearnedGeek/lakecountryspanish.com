using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Tests.Models;

public class EntityTests
{
    [Fact]
    public void ApplicationUser_FullName_CombinesFirstAndLastName()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var fullName = user.FullName;

        // Assert
        Assert.Equal("John Doe", fullName);
    }

    [Fact]
    public void ApplicationUser_HasCustomRate_ReturnsTrueWhenCustomRateSet()
    {
        // Arrange
        var user = new ApplicationUser
        {
            CustomHourlyRate = 20.00m
        };

        // Assert
        Assert.True(user.CustomHourlyRate.HasValue);
        Assert.Equal(20.00m, user.CustomHourlyRate.Value);
    }

    [Fact]
    public void ApplicationUser_HasCustomRate_ReturnsFalseWhenNoCustomRate()
    {
        // Arrange
        var user = new ApplicationUser();

        // Assert
        Assert.False(user.CustomHourlyRate.HasValue);
    }

    [Fact]
    public void Package_PricePerClass_CalculatesCorrectly()
    {
        // Arrange
        var package = new Package
        {
            ClassCount = 10,
            Price = 220.00m
        };

        // Act
        var pricePerClass = package.Price / package.ClassCount;

        // Assert
        Assert.Equal(22.00m, pricePerClass);
    }

    [Fact]
    public void TimeSlot_Duration_CalculatesCorrectly()
    {
        // Arrange
        var timeSlot = new TimeSlot
        {
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0)
        };

        // Act
        var duration = timeSlot.EndTime - timeSlot.StartTime;

        // Assert
        Assert.Equal(TimeSpan.FromHours(1), duration);
    }

    [Fact]
    public void ClassFeedback_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var feedback = new ClassFeedback();

        // Assert
        Assert.False(feedback.AllowPublicDisplay);
        Assert.False(feedback.IsApproved);
        Assert.False(feedback.IsFeatured);
    }

    [Fact]
    public void Tip_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var tip = new Tip();

        // Assert
        Assert.False(tip.IsPaid);
        Assert.Null(tip.ScheduledClassId);
    }

    [Fact]
    public void ScheduledClass_StatusTransitions_AreValid()
    {
        // Arrange
        var scheduledClass = new ScheduledClass
        {
            Status = ClassStatus.Scheduled
        };

        // Act - Complete the class
        scheduledClass.Status = ClassStatus.Completed;

        // Assert
        Assert.Equal(ClassStatus.Completed, scheduledClass.Status);
    }

    [Fact]
    public void StudentPackage_ClassesRemaining_DecrementsProperly()
    {
        // Arrange
        var package = new StudentPackage
        {
            ClassesRemaining = 5
        };

        // Act
        package.ClassesRemaining--;

        // Assert
        Assert.Equal(4, package.ClassesRemaining);
    }

    [Theory]
    [InlineData(1, 25.00, 25.00)]
    [InlineData(5, 115.00, 23.00)]
    [InlineData(10, 220.00, 22.00)]
    [InlineData(20, 420.00, 21.00)]
    public void Package_BulkDiscount_AppliesCorrectly(int classCount, decimal totalPrice, decimal expectedPerClass)
    {
        // Arrange
        var package = new Package
        {
            ClassCount = classCount,
            Price = totalPrice
        };

        // Act
        var pricePerClass = package.Price / package.ClassCount;

        // Assert
        Assert.Equal(expectedPerClass, pricePerClass);
    }
}
