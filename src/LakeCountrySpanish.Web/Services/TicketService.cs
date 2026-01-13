using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for managing class tickets.
/// Tickets are the currency for booking classes (1 ticket = 1 class).
/// </summary>
public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ITokenService _tokenService;
    private readonly ILogger<TicketService> _logger;

    // Token cost for one ticket (configurable)
    private const int DEFAULT_TICKET_TOKEN_COST = 50;

    public TicketService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ITokenService tokenService,
        ILogger<TicketService> logger)
    {
        _context = context;
        _configuration = configuration;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region Ticket Balance Queries

    public async Task<int> GetAvailableTicketCountAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.Tickets
            .CountAsync(t => t.StudentId == studentId &&
                            !t.IsUsed &&
                            (t.ExpiresAt == null || t.ExpiresAt > now));
    }

    public async Task<IEnumerable<Ticket>> GetAvailableTicketsAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.Tickets
            .Where(t => t.StudentId == studentId &&
                       !t.IsUsed &&
                       (t.ExpiresAt == null || t.ExpiresAt > now))
            .OrderBy(t => t.ExpiresAt ?? DateTime.MaxValue) // Expiring first
            .ThenBy(t => t.AcquiredAt) // Then oldest
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetAllTicketsAsync(string studentId, int? limit = null)
    {
        var query = _context.Tickets
            .Where(t => t.StudentId == studentId)
            .OrderByDescending(t => t.AcquiredAt)
            .AsQueryable();

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
    }

    public async Task<Dictionary<TicketSource, int>> GetTicketCountsBySourceAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        var counts = await _context.Tickets
            .Where(t => t.StudentId == studentId &&
                       !t.IsUsed &&
                       (t.ExpiresAt == null || t.ExpiresAt > now))
            .GroupBy(t => t.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.Source, x => x.Count);
    }

    #endregion

    #region Ticket Usage

    public async Task<Ticket?> UseTicketForClassAsync(string studentId, int scheduledClassId)
    {
        var tickets = await GetAvailableTicketsAsync(studentId);
        var ticket = tickets.FirstOrDefault();

        if (ticket == null)
        {
            _logger.LogWarning("No tickets available for student {StudentId}", studentId);
            return null;
        }

        ticket.IsUsed = true;
        ticket.UsedForClassId = scheduledClassId;
        ticket.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Used ticket {TicketId} for class {ClassId} by student {StudentId}",
            ticket.Id, scheduledClassId, studentId);

        return ticket;
    }

    public async Task<bool> RefundTicketAsync(int scheduledClassId)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.UsedForClassId == scheduledClassId && t.IsUsed);

        if (ticket == null)
        {
            _logger.LogWarning("No ticket found for class {ClassId}", scheduledClassId);
            return false;
        }

        ticket.IsUsed = false;
        ticket.UsedForClassId = null;
        ticket.UsedAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Refunded ticket {TicketId} for class {ClassId} to student {StudentId}",
            ticket.Id, scheduledClassId, ticket.StudentId);

        return true;
    }

    #endregion

    #region Token Exchange

    public int GetTicketTokenCost()
    {
        return _configuration.GetValue<int>("TokenStore:TicketCost", DEFAULT_TICKET_TOKEN_COST);
    }

    public async Task<Ticket?> ExchangeTokensForTicketAsync(string studentId)
    {
        var tokenCost = GetTicketTokenCost();
        var tokenBalance = await _tokenService.GetTotalTokenBalanceAsync(studentId);

        if (tokenBalance < tokenCost)
        {
            _logger.LogWarning("Insufficient tokens for student {StudentId}. Has {Balance}, needs {Cost}",
                studentId, tokenBalance, tokenCost);
            return null;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Deduct tokens (use oldest first)
            var tokensToDeduct = tokenCost;
            var activeTokens = await _tokenService.GetActiveTokensAsync(studentId);

            foreach (var token in activeTokens)
            {
                if (tokensToDeduct <= 0) break;

                var deductFromThis = Math.Min(token.QuantityRemaining, tokensToDeduct);
                token.QuantityRemaining -= deductFromThis;
                tokensToDeduct -= deductFromThis;
            }

            // Create token transaction for the exchange
            var currentBalance = await _tokenService.GetTotalTokenBalanceAsync(studentId);
            var txn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.UsedForClass, // Reusing this type for now
                Quantity = -tokenCost,
                BalanceAfter = currentBalance,
                Details = $"Exchanged {tokenCost} tokens for 1 class ticket",
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            // Create the ticket
            var ticket = new Ticket
            {
                StudentId = studentId,
                Source = TicketSource.TokenExchange,
                AcquiredAt = DateTime.UtcNow,
                ExpiresAt = null, // Token-exchanged tickets don't expire
                IsUsed = false,
                TokensSpent = tokenCost,
                Notes = $"Exchanged {tokenCost} tokens for this ticket"
            };
            _context.Tickets.Add(ticket);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Student {StudentId} exchanged {TokenCost} tokens for ticket {TicketId}",
                studentId, tokenCost, ticket.Id);

            return ticket;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error exchanging tokens for ticket for student {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Ticket Purchase

    public async Task<bool> CanPurchaseTicketsAsync(string studentId)
    {
        var permission = await GetActiveTicketPermissionAsync(studentId);
        return permission != null;
    }

    public async Task<TokenPurchasePermission?> GetActiveTicketPermissionAsync(string studentId)
    {
        // Reuse existing TokenPurchasePermission for ticket purchases
        var now = DateTime.UtcNow;
        return await _context.TokenPurchasePermissions
            .FirstOrDefaultAsync(p => p.StudentId == studentId &&
                                      p.IsEnabled &&
                                      p.ExpiresAt > now &&
                                      p.TokensPurchased < p.TokenLimit);
    }

    public async Task<string?> CreateTicketPurchaseCheckoutAsync(
        string studentId,
        int quantity,
        string successUrl,
        string cancelUrl)
    {
        var permission = await GetActiveTicketPermissionAsync(studentId);
        if (permission == null)
        {
            _logger.LogWarning("No active permission for student {StudentId}", studentId);
            return null;
        }

        if (quantity > permission.TokensRemaining)
        {
            _logger.LogWarning("Requested quantity {Quantity} exceeds remaining {Remaining} for student {StudentId}",
                quantity, permission.TokensRemaining, studentId);
            return null;
        }

        var student = await _context.Users.FindAsync(studentId);
        if (student == null)
        {
            _logger.LogError("Student {StudentId} not found", studentId);
            return null;
        }

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(permission.TokenPrice * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Spanish Class Ticket",
                            Description = "Ticket for booking one Spanish class"
                        }
                    },
                    Quantity = quantity
                }
            },
            Mode = "payment",
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            CustomerEmail = student.Email,
            Metadata = new Dictionary<string, string>
            {
                { "student_id", studentId },
                { "quantity", quantity.ToString() },
                { "permission_id", permission.Id.ToString() },
                { "token_validity_days", permission.TokenValidityDays.ToString() },
                { "purchase_type", "ticket" }
            }
        };

        if (!string.IsNullOrEmpty(student.StripeCustomerId))
        {
            options.Customer = student.StripeCustomerId;
            options.CustomerEmail = null;
        }

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        _logger.LogInformation("Created checkout session {SessionId} for {Quantity} tickets for student {StudentId}",
            session.Id, quantity, studentId);

        return session.Url;
    }

    public async Task<IEnumerable<Ticket>> CompletePurchaseAsync(
        string studentId,
        int quantity,
        string stripeSessionId,
        string? paymentIntentId,
        DateTime? expiresAt = null)
    {
        var permission = await GetActiveTicketPermissionAsync(studentId);

        // Calculate expiration if not provided
        var ticketExpiration = expiresAt;
        if (ticketExpiration == null && permission != null)
        {
            ticketExpiration = DateTime.UtcNow.AddDays(permission.TokenValidityDays);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var tickets = new List<Ticket>();

            for (int i = 0; i < quantity; i++)
            {
                var ticket = new Ticket
                {
                    StudentId = studentId,
                    Source = TicketSource.Purchased,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresAt = ticketExpiration,
                    IsUsed = false,
                    StripeSessionId = stripeSessionId,
                    StripePaymentIntentId = paymentIntentId,
                    Notes = $"Purchased via Stripe"
                };
                _context.Tickets.Add(ticket);
                tickets.Add(ticket);
            }

            // Update permission's purchased count
            if (permission != null)
            {
                permission.TokensPurchased += quantity;
                permission.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Completed purchase of {Quantity} tickets for student {StudentId}",
                quantity, studentId);

            return tickets;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error completing ticket purchase for student {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Admin Grant

    public async Task<IEnumerable<Ticket>> GrantTicketsAsync(
        string studentId,
        int quantity,
        DateTime? expiresAt,
        string? notes = null)
    {
        var tickets = new List<Ticket>();

        for (int i = 0; i < quantity; i++)
        {
            var ticket = new Ticket
            {
                StudentId = studentId,
                Source = TicketSource.AdminGrant,
                AcquiredAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false,
                Notes = notes
            };
            _context.Tickets.Add(ticket);
            tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Granted {Quantity} tickets to student {StudentId}", quantity, studentId);

        return tickets;
    }

    #endregion

    #region Subscription Grants

    public async Task<IEnumerable<Ticket>> GrantSubscriptionTicketsAsync(
        string studentId,
        int subscriptionId,
        int quantity,
        DateTime expiresAt)
    {
        var tickets = new List<Ticket>();

        for (int i = 0; i < quantity; i++)
        {
            var ticket = new Ticket
            {
                StudentId = studentId,
                Source = TicketSource.Subscription,
                AcquiredAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false,
                SubscriptionId = subscriptionId,
                Notes = $"Monthly subscription grant"
            };
            _context.Tickets.Add(ticket);
            tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Granted {Quantity} subscription tickets to student {StudentId} from subscription {SubscriptionId}",
            quantity, studentId, subscriptionId);

        return tickets;
    }

    #endregion

    #region Migration

    public async Task<int> MigrateLegacyCreditsAsync(string studentId)
    {
        var studentPackages = await _context.StudentPackages
            .Where(sp => sp.StudentId == studentId && sp.ClassesRemaining > 0)
            .ToListAsync();

        if (!studentPackages.Any())
            return 0;

        var totalMigrated = 0;

        foreach (var package in studentPackages)
        {
            for (int i = 0; i < package.ClassesRemaining; i++)
            {
                var ticket = new Ticket
                {
                    StudentId = studentId,
                    Source = TicketSource.LegacyMigration,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresAt = null, // Legacy credits didn't expire
                    IsUsed = false,
                    Notes = $"Migrated from package #{package.PackageId}"
                };
                _context.Tickets.Add(ticket);
                totalMigrated++;
            }

            // Zero out the legacy credits
            package.ClassesRemaining = 0;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Migrated {Count} legacy credits to tickets for student {StudentId}",
            totalMigrated, studentId);

        return totalMigrated;
    }

    public async Task<int> MigrateAllLegacyCreditsAsync()
    {
        var studentsWithCredits = await _context.StudentPackages
            .Where(sp => sp.ClassesRemaining > 0)
            .Select(sp => sp.StudentId)
            .Distinct()
            .ToListAsync();

        var totalMigrated = 0;

        foreach (var studentId in studentsWithCredits)
        {
            totalMigrated += await MigrateLegacyCreditsAsync(studentId);
        }

        _logger.LogInformation("Migrated {Count} total legacy credits to tickets", totalMigrated);

        return totalMigrated;
    }

    #endregion

    #region Ticket Redemption (Free Class Rewards)

    public async Task<TicketRedemption?> UseTicketForFreeClassAsync(string studentId, int scheduledClassId)
    {
        var tickets = await GetAvailableTicketsAsync(studentId);
        var ticket = tickets.FirstOrDefault();

        if (ticket == null)
        {
            _logger.LogWarning("No tickets available for free class redemption for student {StudentId}", studentId);
            return null;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Mark ticket as used
            ticket.IsUsed = true;
            ticket.UsedForClassId = scheduledClassId;
            ticket.UsedAt = DateTime.UtcNow;

            // Update the scheduled class to mark as paid with ticket
            var scheduledClass = await _context.ScheduledClasses.FindAsync(scheduledClassId);
            if (scheduledClass != null)
            {
                scheduledClass.PaymentStatus = PaymentStatus.PaidWithTicket;
                scheduledClass.TicketId = ticket.Id;
            }

            // Create redemption record
            var redemption = new TicketRedemption
            {
                TicketId = ticket.Id,
                StudentId = studentId,
                Type = TicketRedemptionType.FreeExtraClass,
                ScheduledClassId = scheduledClassId,
                DiscountAmount = 0, // Free class, not a discount
                RedeemedAt = DateTime.UtcNow,
                Notes = "Used ticket for free extra class"
            };
            _context.TicketRedemptions.Add(redemption);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Student {StudentId} used ticket {TicketId} for free class {ClassId}",
                studentId, ticket.Id, scheduledClassId);

            return redemption;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error using ticket for free class for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<IEnumerable<TicketRedemption>> ApplyTicketsAsDiscountAsync(
        string studentId,
        int ticketCount,
        int paymentId,
        decimal discountPerTicket)
    {
        var tickets = (await GetAvailableTicketsAsync(studentId)).Take(ticketCount).ToList();

        if (tickets.Count < ticketCount)
        {
            _logger.LogWarning("Not enough tickets for discount. Requested {Requested}, available {Available}",
                ticketCount, tickets.Count);
            throw new InvalidOperationException($"Not enough tickets. Requested {ticketCount}, but only {tickets.Count} available.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var redemptions = new List<TicketRedemption>();

            foreach (var ticket in tickets)
            {
                // Mark ticket as used
                ticket.IsUsed = true;
                ticket.UsedAt = DateTime.UtcNow;

                // Create redemption record
                var redemption = new TicketRedemption
                {
                    TicketId = ticket.Id,
                    StudentId = studentId,
                    Type = TicketRedemptionType.InvoiceDiscount,
                    PaymentId = paymentId,
                    DiscountAmount = discountPerTicket,
                    RedeemedAt = DateTime.UtcNow,
                    Notes = $"Applied as ${discountPerTicket:F2} discount on payment"
                };
                _context.TicketRedemptions.Add(redemption);
                redemptions.Add(redemption);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Student {StudentId} applied {Count} tickets as ${Discount:F2} discount on payment {PaymentId}",
                studentId, ticketCount, discountPerTicket * ticketCount, paymentId);

            return redemptions;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error applying tickets as discount for student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<IEnumerable<TicketRedemption>> GetRedemptionHistoryAsync(string studentId, int? limit = null)
    {
        var query = _context.TicketRedemptions
            .Include(r => r.Ticket)
            .Include(r => r.ScheduledClass)
            .Include(r => r.Payment)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.RedeemedAt)
            .AsQueryable();

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
    }

    public async Task<(decimal subtotal, decimal ticketDiscount, decimal totalDue)> CalculateCheckoutWithTicketsAsync(
        string studentId,
        int classCount,
        decimal pricePerClass,
        int ticketsToApply)
    {
        // Validate tickets available
        var availableTickets = await GetAvailableTicketCountAsync(studentId);
        var actualTicketsToApply = Math.Min(ticketsToApply, Math.Min(availableTickets, classCount));

        // Calculate totals
        var subtotal = classCount * pricePerClass;
        var ticketDiscount = actualTicketsToApply * pricePerClass; // Each ticket discounts one class
        var totalDue = subtotal - ticketDiscount;

        // Ensure we don't go negative
        if (totalDue < 0) totalDue = 0;

        return (subtotal, ticketDiscount, totalDue);
    }

    #endregion
}
