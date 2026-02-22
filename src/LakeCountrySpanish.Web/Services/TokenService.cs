using LakeCountrySpanish.Web.Data;
using LakeCountrySpanish.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace LakeCountrySpanish.Web.Services;

/// <summary>
/// Service for managing tokens and token purchase permissions.
/// </summary>
public class TokenService : ITokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    #region Token Balance Queries

    public async Task<int> GetEarnedTokenBalanceAsync(string studentId)
    {
        return await _context.Tokens
            .Where(t => t.StudentId == studentId &&
                        t.Source == TokenSource.Earned &&
                        t.QuantityRemaining > 0)
            .SumAsync(t => t.QuantityRemaining);
    }

    public async Task<int> GetPurchasedTokenBalanceAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.Tokens
            .Where(t => t.StudentId == studentId &&
                        t.Source == TokenSource.Purchased &&
                        t.QuantityRemaining > 0 &&
                        (t.ExpiresAt == null || t.ExpiresAt > now))
            .SumAsync(t => t.QuantityRemaining);
    }

    public async Task<int> GetTotalTokenBalanceAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.Tokens
            .Where(t => t.StudentId == studentId &&
                        t.QuantityRemaining > 0 &&
                        (t.ExpiresAt == null || t.ExpiresAt > now))
            .SumAsync(t => t.QuantityRemaining);
    }

    public async Task<IEnumerable<Models.Entities.Token>> GetActiveTokensAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.Tokens
            .Where(t => t.StudentId == studentId &&
                        t.QuantityRemaining > 0 &&
                        (t.ExpiresAt == null || t.ExpiresAt > now))
            .OrderBy(t => t.ExpiresAt ?? DateTime.MaxValue)  // Expiring tokens first
            .ThenBy(t => t.CreatedAt)  // Then by creation date
            .ToListAsync();
    }

    #endregion

    #region Token Purchase Permission

    public async Task<TokenPurchasePermission?> GetActivePermissionAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        return await _context.TokenPurchasePermissions
            .FirstOrDefaultAsync(p => p.StudentId == studentId &&
                                      p.IsEnabled &&
                                      p.ExpiresAt > now &&
                                      p.TokensPurchased < p.TokenLimit);
    }

    public async Task<IEnumerable<TokenPurchasePermission>> GetPermissionHistoryAsync(string studentId)
    {
        return await _context.TokenPurchasePermissions
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> CanPurchaseTokensAsync(string studentId)
    {
        var permission = await GetActivePermissionAsync(studentId);
        return permission != null;
    }

    #endregion

    #region Token Purchase

    public async Task<string?> CreateTokenPurchaseCheckoutAsync(
        string studentId,
        int quantity,
        string successUrl,
        string cancelUrl)
    {
        var permission = await GetActivePermissionAsync(studentId);
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

        var totalAmount = (long)(permission.TokenPrice * quantity * 100); // Convert to cents

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
                            Name = "Spanish Class Token",
                            Description = $"Token for booking Spanish classes (expires in {permission.TokenValidityDays} days after purchase)"
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
                { "purchase_type", "token" }
            }
        };

        // Use existing Stripe customer if available
        if (!string.IsNullOrEmpty(student.StripeCustomerId))
        {
            options.Customer = student.StripeCustomerId;
            options.CustomerEmail = null;
        }

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        _logger.LogInformation("Created checkout session {SessionId} for {Quantity} tokens for student {StudentId}",
            session.Id, quantity, studentId);

        return session.Url;
    }

    public async Task<Models.Entities.Token?> CompletePurchaseAsync(
        string studentId,
        int quantity,
        string stripeSessionId,
        string? paymentIntentId)
    {
        var permission = await GetActivePermissionAsync(studentId);
        if (permission == null)
        {
            _logger.LogError("No active permission for student {StudentId} during purchase completion", studentId);
            return null;
        }

        // Calculate expiration based on permission's validity days
        var expiresAt = DateTime.UtcNow.AddDays(permission.TokenValidityDays);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create the token batch
            var token = new Models.Entities.Token
            {
                StudentId = studentId,
                Source = TokenSource.Purchased,
                Quantity = quantity,
                QuantityRemaining = quantity,
                ExpiresAt = expiresAt,
                TokenPurchasePermissionId = permission.Id,
                StripeSessionId = stripeSessionId,
                StripePaymentIntentId = paymentIntentId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tokens.Add(token);

            // Update permission's purchased count
            permission.TokensPurchased += quantity;
            permission.UpdatedAt = DateTime.UtcNow;

            // Get current balance for transaction record
            var currentBalance = await GetTotalTokenBalanceAsync(studentId);

            // Create transaction record
            var txn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.Purchased,
                Quantity = quantity,
                BalanceAfter = currentBalance + quantity,
                TokenId = null, // Will update after save
                Details = $"Purchased {quantity} token(s) via Stripe session {stripeSessionId}",
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            await _context.SaveChangesAsync();

            // Update transaction with token ID
            txn.TokenId = token.Id;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Completed purchase of {Quantity} tokens for student {StudentId}, token batch {TokenId}",
                quantity, studentId, token.Id);

            return token;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error completing token purchase for student {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Token Usage

    public async Task<bool> UseTokenForClassAsync(string studentId, int scheduledClassId)
    {
        var tokens = await GetActiveTokensAsync(studentId);
        var tokenList = tokens.ToList();

        if (!tokenList.Any())
        {
            _logger.LogWarning("No tokens available for student {StudentId}", studentId);
            return false;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Use from oldest expiring token first
            var token = tokenList.First();
            token.QuantityRemaining--;

            var currentBalance = await GetTotalTokenBalanceAsync(studentId);

            var txn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.UsedForClass,
                Quantity = -1,
                BalanceAfter = currentBalance - 1,
                TokenId = token.Id,
                ScheduledClassId = scheduledClassId,
                Details = $"Used for class #{scheduledClassId}",
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Used 1 token from batch {TokenId} for class {ClassId} by student {StudentId}",
                token.Id, scheduledClassId, studentId);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error using token for class {ClassId} by student {StudentId}", scheduledClassId, studentId);
            throw;
        }
    }

    public async Task<bool> RefundTokenAsync(string studentId, int scheduledClassId)
    {
        // Find the original transaction for this class
        var originalTxn = await _context.TokenTransactions
            .FirstOrDefaultAsync(t => t.StudentId == studentId &&
                                      t.ScheduledClassId == scheduledClassId &&
                                      t.Type == TokenTransactionType.UsedForClass);

        if (originalTxn == null)
        {
            _logger.LogWarning("No token transaction found for class {ClassId} by student {StudentId}", scheduledClassId, studentId);
            return false;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Restore token to original batch if possible
            if (originalTxn.TokenId.HasValue)
            {
                var token = await _context.Tokens.FindAsync(originalTxn.TokenId.Value);
                if (token != null)
                {
                    token.QuantityRemaining++;
                }
            }

            var currentBalance = await GetTotalTokenBalanceAsync(studentId);

            var refundTxn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.Refunded,
                Quantity = 1,
                BalanceAfter = currentBalance + 1,
                TokenId = originalTxn.TokenId,
                ScheduledClassId = scheduledClassId,
                Details = $"Refunded for cancelled class #{scheduledClassId}",
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(refundTxn);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Refunded 1 token for class {ClassId} to student {StudentId}", scheduledClassId, studentId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error refunding token for class {ClassId} by student {StudentId}", scheduledClassId, studentId);
            throw;
        }
    }

    public async Task<bool> ForfeitTokenAsync(string studentId, int scheduledClassId)
    {
        // Find the original transaction for this class
        var originalTxn = await _context.TokenTransactions
            .FirstOrDefaultAsync(t => t.StudentId == studentId &&
                                      t.ScheduledClassId == scheduledClassId &&
                                      t.Type == TokenTransactionType.UsedForClass);

        if (originalTxn == null)
        {
            _logger.LogWarning("No token transaction found for class {ClassId} by student {StudentId}", scheduledClassId, studentId);
            return false;
        }

        var currentBalance = await GetTotalTokenBalanceAsync(studentId);

        // Record the forfeiture (token is not refunded)
        var forfeitTxn = new TokenTransaction
        {
            StudentId = studentId,
            Type = TokenTransactionType.Forfeited,
            Quantity = 0, // No change - already deducted
            BalanceAfter = currentBalance,
            TokenId = originalTxn.TokenId,
            ScheduledClassId = scheduledClassId,
            Details = $"Forfeited for no-show/late cancellation of class #{scheduledClassId}",
            CreatedAt = DateTime.UtcNow
        };
        _context.TokenTransactions.Add(forfeitTxn);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Token forfeited for class {ClassId} by student {StudentId}", scheduledClassId, studentId);
        return true;
    }

    #endregion

    #region Token Earning

    public async Task<Models.Entities.Token> AwardEarnedTokensAsync(string studentId, int quantity, string details)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create earned token batch (never expires)
            var token = new Models.Entities.Token
            {
                StudentId = studentId,
                Source = TokenSource.Earned,
                Quantity = quantity,
                QuantityRemaining = quantity,
                ExpiresAt = null, // Never expires
                CreatedAt = DateTime.UtcNow
            };
            _context.Tokens.Add(token);

            var currentBalance = await GetTotalTokenBalanceAsync(studentId);

            var txn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.Earned,
                Quantity = quantity,
                BalanceAfter = currentBalance + quantity,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            await _context.SaveChangesAsync();

            // Update transaction with token ID
            txn.TokenId = token.Id;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Awarded {Quantity} earned tokens to student {StudentId}", quantity, studentId);
            return token;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error awarding earned tokens to student {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Admin Functions

    public async Task<TokenPurchasePermission> GrantPurchasePermissionAsync(
        string studentId,
        int tokenLimit,
        DateTime expiresAt,
        PermissionReason reason,
        int tokenValidityDays,
        decimal tokenPrice,
        string adminId,
        string? notes = null)
    {
        var permission = new TokenPurchasePermission
        {
            StudentId = studentId,
            IsEnabled = true,
            TokenLimit = tokenLimit,
            TokensPurchased = 0,
            ExpiresAt = expiresAt,
            TokenValidityDays = tokenValidityDays,
            TokenPrice = tokenPrice,
            Reason = reason,
            AdminNotes = notes,
            GrantedById = adminId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TokenPurchasePermissions.Add(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Granted purchase permission {PermissionId} to student {StudentId} for {Limit} tokens",
            permission.Id, studentId, tokenLimit);

        return permission;
    }

    public async Task<TokenPurchasePermission?> UpdatePurchasePermissionAsync(
        int permissionId,
        bool isEnabled,
        int? tokenLimit = null,
        DateTime? expiresAt = null,
        string? notes = null)
    {
        var permission = await _context.TokenPurchasePermissions.FindAsync(permissionId);
        if (permission == null)
            return null;

        permission.IsEnabled = isEnabled;
        if (tokenLimit.HasValue)
            permission.TokenLimit = tokenLimit.Value;
        if (expiresAt.HasValue)
            permission.ExpiresAt = expiresAt.Value;
        if (notes != null)
            permission.AdminNotes = notes;
        permission.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated permission {PermissionId}", permissionId);
        return permission;
    }

    public async Task<bool> DisablePermissionAsync(int permissionId)
    {
        var permission = await _context.TokenPurchasePermissions.FindAsync(permissionId);
        if (permission == null)
            return false;

        permission.IsEnabled = false;
        permission.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Disabled permission {PermissionId}", permissionId);
        return true;
    }

    public async Task<Models.Entities.Token> GrantTokensAsync(
        string studentId,
        int quantity,
        DateTime? expiresAt,
        string adminId,
        string? notes = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var token = new Models.Entities.Token
            {
                StudentId = studentId,
                Source = TokenSource.AdminGrant,
                Quantity = quantity,
                QuantityRemaining = quantity,
                ExpiresAt = expiresAt,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };
            _context.Tokens.Add(token);

            var currentBalance = await GetTotalTokenBalanceAsync(studentId);

            var txn = new TokenTransaction
            {
                StudentId = studentId,
                Type = TokenTransactionType.AdminGrant,
                Quantity = quantity,
                BalanceAfter = currentBalance + quantity,
                Details = $"Admin granted {quantity} token(s). {notes ?? ""}".Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            await _context.SaveChangesAsync();

            txn.TokenId = token.Id;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Admin {AdminId} granted {Quantity} tokens to student {StudentId}",
                adminId, quantity, studentId);

            return token;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error granting tokens to student {StudentId}", studentId);
            throw;
        }
    }

    #endregion

    #region Transaction History

    public async Task<IEnumerable<TokenTransaction>> GetTransactionHistoryAsync(string studentId, int? limit = null)
    {
        var query = _context.TokenTransactions
            .Where(t => t.StudentId == studentId)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync();
    }

    #endregion

    #region Expiration Handling

    public async Task<int> ExpireTokensAsync()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = await _context.Tokens
            .Where(t => t.ExpiresAt != null &&
                        t.ExpiresAt <= now &&
                        t.QuantityRemaining > 0)
            .ToListAsync();

        if (!expiredTokens.Any())
            return 0;

        // Pre-fetch balances for all affected students in a single query (avoids N+1)
        var studentIds = expiredTokens.Select(t => t.StudentId).Distinct().ToList();
        var balances = await _context.Tokens
            .Where(t => studentIds.Contains(t.StudentId) &&
                        t.QuantityRemaining > 0 &&
                        (t.ExpiresAt == null || t.ExpiresAt > now))
            .GroupBy(t => t.StudentId)
            .Select(g => new { StudentId = g.Key, Balance = g.Sum(t => t.QuantityRemaining) })
            .ToDictionaryAsync(x => x.StudentId, x => x.Balance);

        var expiredCount = 0;

        foreach (var token in expiredTokens)
        {
            var expiredQty = token.QuantityRemaining;
            var currentBalance = balances.GetValueOrDefault(token.StudentId, 0);

            var txn = new TokenTransaction
            {
                StudentId = token.StudentId,
                Type = TokenTransactionType.Expired,
                Quantity = -expiredQty,
                BalanceAfter = currentBalance - expiredQty,
                TokenId = token.Id,
                Details = $"Expired {expiredQty} token(s) from batch #{token.Id}",
                CreatedAt = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(txn);

            token.QuantityRemaining = 0;
            // Update in-memory balance to keep subsequent tokens for the same student accurate
            balances[token.StudentId] = currentBalance - expiredQty;
            expiredCount += expiredQty;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Expired {Count} tokens across {Batches} batches",
            expiredCount, expiredTokens.Count);

        return expiredCount;
    }

    #endregion
}
