using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services;

namespace LakeCountrySpanish.Tests.Services;

/// <summary>
/// Tests for the TicketService which handles class ticket operations.
/// Tickets are the currency for booking classes (1 ticket = 1 class).
/// </summary>
public class TicketServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<TicketService>> _loggerMock;
    private readonly TicketService _ticketService;

    public TicketServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<TicketService>>();

        // Use real configuration with in-memory values
        var configValues = new Dictionary<string, string?>
        {
            { "TokenStore:TicketCost", "50" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _ticketService = new TicketService(
            _context,
            configuration,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    #region GetAvailableTicketCountAsync Tests

    [Fact]
    public async Task GetAvailableTicketCountAsync_WithNoTickets_ReturnsZero()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        // Act
        var count = await _ticketService.GetAvailableTicketCountAsync(student.Id);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetAvailableTicketCountAsync_WithAvailableTickets_ReturnsCorrectCount()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tickets = new List<Ticket>
        {
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = true, AcquiredAt = DateTime.UtcNow } // Used ticket
        };
        _context.Tickets.AddRange(tickets);
        await _context.SaveChangesAsync();

        // Act
        var count = await _ticketService.GetAvailableTicketCountAsync(student.Id);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetAvailableTicketCountAsync_WithExpiredTickets_ExcludesExpired()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tickets = new List<Ticket>
        {
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(30), AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(-1), AcquiredAt = DateTime.UtcNow } // Expired
        };
        _context.Tickets.AddRange(tickets);
        await _context.SaveChangesAsync();

        // Act
        var count = await _ticketService.GetAvailableTicketCountAsync(student.Id);

        // Assert
        Assert.Equal(1, count);
    }

    #endregion

    #region GetAvailableTicketsAsync Tests

    [Fact]
    public async Task GetAvailableTicketsAsync_OrdersByExpirationThenAcquiredDate()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tickets = new List<Ticket>
        {
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, ExpiresAt = null, AcquiredAt = DateTime.UtcNow.AddDays(-10) }, // Oldest, no expiration
            new() { StudentId = student.Id, Source = TicketSource.Subscription, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(7), AcquiredAt = DateTime.UtcNow }, // Expiring soonest
            new() { StudentId = student.Id, Source = TicketSource.Purchased, IsUsed = false, ExpiresAt = DateTime.UtcNow.AddDays(30), AcquiredAt = DateTime.UtcNow.AddDays(-5) }
        };
        _context.Tickets.AddRange(tickets);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _ticketService.GetAvailableTicketsAsync(student.Id)).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(TicketSource.Subscription, result[0].Source); // Expiring soonest first
        Assert.Equal(TicketSource.Purchased, result[1].Source); // Next to expire
        Assert.Equal(TicketSource.AdminGrant, result[2].Source); // No expiration last
    }

    #endregion

    #region UseTicketForClassAsync Tests

    [Fact]
    public async Task UseTicketForClassAsync_WithAvailableTicket_MarksTicketUsed()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var ticket = new Ticket
        {
            StudentId = student.Id,
            Source = TicketSource.AdminGrant,
            IsUsed = false,
            AcquiredAt = DateTime.UtcNow
        };
        _context.Tickets.Add(ticket);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var usedTicket = await _ticketService.UseTicketForClassAsync(student.Id, scheduledClass.Id);

        // Assert
        Assert.NotNull(usedTicket);
        Assert.True(usedTicket.IsUsed);
        Assert.Equal(scheduledClass.Id, usedTicket.UsedForClassId);
        Assert.NotNull(usedTicket.UsedAt);
    }

    [Fact]
    public async Task UseTicketForClassAsync_WithNoAvailableTickets_ReturnsNull()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.UseTicketForClassAsync(student.Id, scheduledClass.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UseTicketForClassAsync_UsesOldestExpiringFirst()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        // Ticket expiring in 7 days (should be used first)
        var expiringTicket = new Ticket
        {
            StudentId = student.Id,
            Source = TicketSource.Subscription,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            AcquiredAt = DateTime.UtcNow
        };
        // Ticket expiring in 30 days
        var laterTicket = new Ticket
        {
            StudentId = student.Id,
            Source = TicketSource.Purchased,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            AcquiredAt = DateTime.UtcNow.AddDays(-5)
        };
        _context.Tickets.AddRange(expiringTicket, laterTicket);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        // Act
        var usedTicket = await _ticketService.UseTicketForClassAsync(student.Id, scheduledClass.Id);

        // Assert
        Assert.NotNull(usedTicket);
        Assert.Equal(TicketSource.Subscription, usedTicket.Source); // Expiring soonest
    }

    #endregion

    #region RefundTicketAsync Tests

    [Fact]
    public async Task RefundTicketAsync_WithUsedTicket_RestoresTicket()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var timeSlot = CreateTestTimeSlot();
        _context.TimeSlots.Add(timeSlot);
        await _context.SaveChangesAsync();

        var scheduledClass = new ScheduledClass
        {
            StudentId = student.Id,
            TimeSlotId = timeSlot.Id,
            ClassDateTime = DateTime.UtcNow.AddDays(7),
            Status = ClassStatus.Scheduled
        };
        _context.ScheduledClasses.Add(scheduledClass);
        await _context.SaveChangesAsync();

        var ticket = new Ticket
        {
            StudentId = student.Id,
            Source = TicketSource.AdminGrant,
            IsUsed = true,
            UsedForClassId = scheduledClass.Id,
            UsedAt = DateTime.UtcNow,
            AcquiredAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ticketService.RefundTicketAsync(scheduledClass.Id);

        // Assert
        Assert.True(result);
        var refundedTicket = await _context.Tickets.FindAsync(ticket.Id);
        Assert.False(refundedTicket!.IsUsed);
        Assert.Null(refundedTicket.UsedForClassId);
        Assert.Null(refundedTicket.UsedAt);
    }

    [Fact]
    public async Task RefundTicketAsync_WithNoTicketForClass_ReturnsFalse()
    {
        // Arrange - no ticket associated with class ID 999

        // Act
        var result = await _ticketService.RefundTicketAsync(999);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GrantTicketsAsync Tests

    [Fact]
    public async Task GrantTicketsAsync_CreatesTicketsWithCorrectSource()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.GrantTicketsAsync(
            student.Id,
            quantity: 3,
            expiresAt: DateTime.UtcNow.AddDays(30),
            notes: "Test grant");

        // Assert
        var grantedTickets = tickets.ToList();
        Assert.Equal(3, grantedTickets.Count);
        Assert.All(grantedTickets, t =>
        {
            Assert.Equal(TicketSource.AdminGrant, t.Source);
            Assert.Equal(student.Id, t.StudentId);
            Assert.False(t.IsUsed);
            Assert.Equal("Test grant", t.Notes);
        });
    }

    [Fact]
    public async Task GrantTicketsAsync_WithNoExpiration_CreatesNonExpiringTickets()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        // Act
        var tickets = await _ticketService.GrantTicketsAsync(
            student.Id,
            quantity: 2,
            expiresAt: null,
            notes: "Comp ticket");

        // Assert
        var grantedTickets = tickets.ToList();
        Assert.All(grantedTickets, t => Assert.Null(t.ExpiresAt));
    }

    #endregion

    #region GetTicketCountsBySourceAsync Tests

    [Fact]
    public async Task GetTicketCountsBySourceAsync_GroupsTicketsBySource()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tickets = new List<Ticket>
        {
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.Subscription, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.TokenExchange, IsUsed = false, AcquiredAt = DateTime.UtcNow }
        };
        _context.Tickets.AddRange(tickets);
        await _context.SaveChangesAsync();

        // Act
        var counts = await _ticketService.GetTicketCountsBySourceAsync(student.Id);

        // Assert
        Assert.Equal(3, counts.Count);
        Assert.Equal(2, counts[TicketSource.AdminGrant]);
        Assert.Equal(1, counts[TicketSource.Subscription]);
        Assert.Equal(1, counts[TicketSource.TokenExchange]);
    }

    [Fact]
    public async Task GetTicketCountsBySourceAsync_ExcludesUsedTickets()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);

        var tickets = new List<Ticket>
        {
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = false, AcquiredAt = DateTime.UtcNow },
            new() { StudentId = student.Id, Source = TicketSource.AdminGrant, IsUsed = true, AcquiredAt = DateTime.UtcNow } // Used
        };
        _context.Tickets.AddRange(tickets);
        await _context.SaveChangesAsync();

        // Act
        var counts = await _ticketService.GetTicketCountsBySourceAsync(student.Id);

        // Assert
        Assert.Single(counts);
        Assert.Equal(1, counts[TicketSource.AdminGrant]);
    }

    #endregion

    #region ExchangeTokensForTicketAsync Tests

    [Fact]
    public async Task ExchangeTokensForTicketAsync_WithSufficientTokens_CreatesTicket()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var tokenCost = 50;
        _tokenServiceMock.Setup(t => t.GetTotalTokenBalanceAsync(student.Id))
            .ReturnsAsync(100); // Has enough tokens
        _tokenServiceMock.Setup(t => t.GetActiveTokensAsync(student.Id))
            .ReturnsAsync(new List<Token>
            {
                new() { StudentId = student.Id, Source = TokenSource.Earned, Quantity = 100, QuantityRemaining = 100 }
            });

        // Act
        var ticket = await _ticketService.ExchangeTokensForTicketAsync(student.Id);

        // Assert
        Assert.NotNull(ticket);
        Assert.Equal(TicketSource.TokenExchange, ticket.Source);
        Assert.Equal(tokenCost, ticket.TokensSpent);
        Assert.Null(ticket.ExpiresAt); // Token-exchanged tickets don't expire
    }

    [Fact]
    public async Task ExchangeTokensForTicketAsync_WithInsufficientTokens_ReturnsNull()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _tokenServiceMock.Setup(t => t.GetTotalTokenBalanceAsync(student.Id))
            .ReturnsAsync(30); // Not enough tokens (need 50)

        // Act
        var ticket = await _ticketService.ExchangeTokensForTicketAsync(student.Id);

        // Assert
        Assert.Null(ticket);
    }

    #endregion

    #region GrantSubscriptionTicketsAsync Tests

    [Fact]
    public async Task GrantSubscriptionTicketsAsync_CreatesTicketsWithSubscriptionSource()
    {
        // Arrange
        var student = CreateTestStudent();
        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        var subscriptionId = 123;
        var expiresAt = DateTime.UtcNow.AddMonths(1);

        // Act
        var tickets = await _ticketService.GrantSubscriptionTicketsAsync(
            student.Id,
            subscriptionId,
            quantity: 4,
            expiresAt);

        // Assert
        var grantedTickets = tickets.ToList();
        Assert.Equal(4, grantedTickets.Count);
        Assert.All(grantedTickets, t =>
        {
            Assert.Equal(TicketSource.Subscription, t.Source);
            Assert.Equal(subscriptionId, t.SubscriptionId);
            Assert.Equal(expiresAt, t.ExpiresAt);
        });
    }

    #endregion

    #region Ticket Entity Tests

    [Fact]
    public void Ticket_IsAvailable_WhenNotUsedAndNotExpired()
    {
        // Arrange
        var ticket = new Ticket
        {
            StudentId = "test",
            Source = TicketSource.AdminGrant,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            AcquiredAt = DateTime.UtcNow
        };

        // Assert
        Assert.True(ticket.IsAvailable);
    }

    [Fact]
    public void Ticket_IsNotAvailable_WhenUsed()
    {
        // Arrange
        var ticket = new Ticket
        {
            StudentId = "test",
            Source = TicketSource.AdminGrant,
            IsUsed = true,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            AcquiredAt = DateTime.UtcNow
        };

        // Assert
        Assert.False(ticket.IsAvailable);
    }

    [Fact]
    public void Ticket_IsNotAvailable_WhenExpired()
    {
        // Arrange
        var ticket = new Ticket
        {
            StudentId = "test",
            Source = TicketSource.AdminGrant,
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
            AcquiredAt = DateTime.UtcNow.AddDays(-30)
        };

        // Assert
        Assert.False(ticket.IsAvailable);
    }

    [Fact]
    public void Ticket_IsAvailable_WhenNoExpiration()
    {
        // Arrange
        var ticket = new Ticket
        {
            StudentId = "test",
            Source = TicketSource.AdminGrant,
            IsUsed = false,
            ExpiresAt = null, // No expiration
            AcquiredAt = DateTime.UtcNow
        };

        // Assert
        Assert.True(ticket.IsAvailable);
    }

    #endregion

    #region Helper Methods

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

    private TimeSlot CreateTestTimeSlot()
    {
        return new TimeSlot
        {
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            IsRecurring = true,
            IsActive = true
        };
    }

    #endregion
}
