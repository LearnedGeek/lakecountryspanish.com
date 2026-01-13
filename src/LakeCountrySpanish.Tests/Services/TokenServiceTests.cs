using Microsoft.EntityFrameworkCore;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for token-related data operations.
/// </summary>
public class TokenServiceTests
{
    private readonly ApplicationDbContext _context;

    public TokenServiceTests()
    {
        _context = TestDbContextFactory.Create();
    }

    [Fact]
    public async Task Token_EarnedTokens_CalculateTotalBalance()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var earnedToken = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Earned,
            Quantity = 10,
            QuantityRemaining = 8,
            ExpiresAt = null, // Earned tokens don't expire
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.Add(earnedToken);
        await _context.SaveChangesAsync();

        // Act - Active earned tokens have remaining > 0 and no expiration or future expiration
        var totalEarned = await _context.Tokens
            .Where(t => t.StudentId == student.Id &&
                   t.Source == TokenSource.Earned &&
                   t.QuantityRemaining > 0)
            .SumAsync(t => t.QuantityRemaining);

        // Assert
        Assert.Equal(8, totalEarned);
    }

    [Fact]
    public async Task Token_PurchasedTokens_HaveExpiration()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var purchasedToken = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Purchased,
            Quantity = 20,
            QuantityRemaining = 20,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.Add(purchasedToken);
        await _context.SaveChangesAsync();

        // Act
        var token = await _context.Tokens.FindAsync(purchasedToken.Id);

        // Assert
        Assert.NotNull(token?.ExpiresAt);
        Assert.True(token.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Token_CombinedBalance_SumsEarnedAndPurchased()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var earnedToken = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Earned,
            Quantity = 10,
            QuantityRemaining = 10,
            CreatedAt = DateTime.UtcNow
        };
        var purchasedToken = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Purchased,
            Quantity = 5,
            QuantityRemaining = 5,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.AddRange(earnedToken, purchasedToken);
        await _context.SaveChangesAsync();

        // Act - Query active tokens (remaining > 0 and not expired)
        var totalBalance = await _context.Tokens
            .Where(t => t.StudentId == student.Id &&
                   t.QuantityRemaining > 0 &&
                   (t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow))
            .SumAsync(t => t.QuantityRemaining);

        // Assert
        Assert.Equal(15, totalBalance);
    }

    [Fact]
    public async Task TokenPurchasePermission_WhenActive_AllowsPurchase()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var permission = new TokenPurchasePermission
        {
            StudentId = student.Id,
            IsEnabled = true,
            TokenLimit = 50,
            TokensPurchased = 10,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Reason = PermissionReason.Admin,
            TokenValidityDays = 90,
            TokenPrice = 1.00m,
            GrantedById = Guid.NewGuid().ToString()
        };
        _context.TokenPurchasePermissions.Add(permission);
        await _context.SaveChangesAsync();

        // Act
        var activePermission = await _context.TokenPurchasePermissions
            .Where(p => p.StudentId == student.Id && p.IsEnabled && p.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(activePermission);
        Assert.Equal(40, activePermission.TokensRemaining); // 50 - 10 remaining
    }

    [Fact]
    public async Task TokenPurchasePermission_WhenExpired_DoesNotAllowPurchase()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var permission = new TokenPurchasePermission
        {
            StudentId = student.Id,
            IsEnabled = true,
            TokenLimit = 50,
            TokensPurchased = 0,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
            Reason = PermissionReason.Admin,
            TokenValidityDays = 90,
            TokenPrice = 1.00m,
            GrantedById = Guid.NewGuid().ToString()
        };
        _context.TokenPurchasePermissions.Add(permission);
        await _context.SaveChangesAsync();

        // Act
        var activePermission = await _context.TokenPurchasePermissions
            .Where(p => p.StudentId == student.Id && p.IsEnabled && p.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();

        // Assert
        Assert.Null(activePermission);
    }

    [Fact]
    public async Task TokenTransaction_RecordsUsage()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var transactions = new List<TokenTransaction>
        {
            new()
            {
                StudentId = student.Id,
                Type = TokenTransactionType.Earned,
                Quantity = 10,
                BalanceAfter = 10,
                Details = "Converted 1000 points",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                StudentId = student.Id,
                Type = TokenTransactionType.UsedForClass,
                Quantity = -1,
                BalanceAfter = 9,
                Details = "Booked class on Monday",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };
        _context.TokenTransactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act
        var history = await _context.TokenTransactions
            .Where(t => t.StudentId == student.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Assert
        Assert.Equal(2, history.Count);
        Assert.Equal(TokenTransactionType.UsedForClass, history.First().Type);
        Assert.Equal(-1, history.First().Quantity);
    }

    [Fact]
    public async Task Token_DecrementRemaining_Works()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var token = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Purchased,
            Quantity = 5,
            QuantityRemaining = 5,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.Add(token);
        await _context.SaveChangesAsync();

        // Act - Use one token
        token.QuantityRemaining--;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Tokens.FindAsync(token.Id);
        Assert.Equal(4, updated?.QuantityRemaining);
    }

    [Fact]
    public async Task Token_WhenDepleted_IsNotActive()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var token = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.Purchased,
            Quantity = 1,
            QuantityRemaining = 0, // Depleted
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.Add(token);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Tokens.FindAsync(token.Id);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsActive); // IsActive is computed property
    }

    [Fact]
    public async Task Token_GrantedByAdmin_HasCorrectSource()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var grantedToken = new Token
        {
            StudentId = student.Id,
            Source = TokenSource.AdminGrant,
            Quantity = 3,
            QuantityRemaining = 3,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Notes = "Compensation for technical issue",
            CreatedAt = DateTime.UtcNow
        };
        _context.Tokens.Add(grantedToken);
        await _context.SaveChangesAsync();

        // Act
        var token = await _context.Tokens.FindAsync(grantedToken.Id);

        // Assert
        Assert.Equal(TokenSource.AdminGrant, token?.Source);
        Assert.Contains("Compensation", token?.Notes);
    }

    [Fact]
    public async Task TokenTransaction_TracksClassBooking()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled,
            PaymentStatus = PaymentStatus.Unpaid
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Record token usage via transaction
        var transaction = new TokenTransaction
        {
            StudentId = student.Id,
            Type = TokenTransactionType.UsedForClass,
            Quantity = -1,
            BalanceAfter = 4,
            ScheduledClassId = scheduledClass.Id,
            Details = $"Used for class on {scheduledClass.ClassDateTime:MMM d}"
        };
        _context.TokenTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var usage = await _context.TokenTransactions
            .Include(t => t.ScheduledClass)
            .FirstOrDefaultAsync(t => t.ScheduledClassId == scheduledClass.Id);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(-1, usage.Quantity);
        Assert.Equal(ClassStatus.Scheduled, usage.ScheduledClass?.Status);
    }

    private ApplicationUser CreateTestStudent()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"test{Guid.NewGuid():N}@test.com",
            Email = $"test{Guid.NewGuid():N}@test.com",
            FirstName = "Test",
            LastName = "Student"
        };
    }
}
