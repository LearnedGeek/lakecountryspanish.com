using LakeCountrySpanish.Web.Models.ViewModels;
using LakeCountrySpanish.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LakeCountrySpanish.Web.Controllers;

/// <summary>
/// Controller for student token management and purchases.
/// </summary>
[Authorize]
public class TokenController : Controller
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(
        ITokenService tokenService,
        ILogger<TokenController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Display token balance and purchase options.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();

        var earnedBalance = await _tokenService.GetEarnedTokenBalanceAsync(userId);
        var purchasedBalance = await _tokenService.GetPurchasedTokenBalanceAsync(userId);
        var permission = await _tokenService.GetActivePermissionAsync(userId);
        var tokens = await _tokenService.GetActiveTokensAsync(userId);
        var transactions = await _tokenService.GetTransactionHistoryAsync(userId, 20);

        var viewModel = new TokenViewModel
        {
            EarnedTokens = earnedBalance,
            PurchasedTokens = purchasedBalance,
            TotalTokens = earnedBalance + purchasedBalance,
            ActivePermission = permission,
            CanPurchase = permission != null,
            TokenBatches = tokens.Select(t => new TokenBatchViewModel
            {
                Id = t.Id,
                Source = t.Source.ToString(),
                Quantity = t.Quantity,
                QuantityRemaining = t.QuantityRemaining,
                ExpiresAt = t.ExpiresAt,
                CreatedAt = t.CreatedAt
            }).ToList(),
            RecentTransactions = transactions.Select(t => new TokenTransactionViewModel
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Quantity = t.Quantity,
                BalanceAfter = t.BalanceAfter,
                Details = t.Details,
                CreatedAt = t.CreatedAt
            }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Start token purchase checkout.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(int quantity)
    {
        var userId = GetUserId();

        if (quantity < 1 || quantity > 10)
        {
            TempData["Error"] = "Please select between 1 and 10 tokens.";
            return RedirectToAction(nameof(Index));
        }

        var canPurchase = await _tokenService.CanPurchaseTokensAsync(userId);
        if (!canPurchase)
        {
            TempData["Error"] = "You do not have permission to purchase tokens. Please contact Karen for assistance.";
            return RedirectToAction(nameof(Index));
        }

        var permission = await _tokenService.GetActivePermissionAsync(userId);
        if (permission != null && quantity > permission.TokensRemaining)
        {
            TempData["Error"] = $"You can only purchase up to {permission.TokensRemaining} more token(s) under your current permission.";
            return RedirectToAction(nameof(Index));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/Token/Success";
        var cancelUrl = $"{baseUrl}/Token";

        var checkoutUrl = await _tokenService.CreateTokenPurchaseCheckoutAsync(
            userId, quantity, successUrl, cancelUrl);

        if (checkoutUrl == null)
        {
            TempData["Error"] = "Unable to create checkout session. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        return Redirect(checkoutUrl);
    }

    /// <summary>
    /// Handle successful purchase return from Stripe.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Success(string session_id)
    {
        if (string.IsNullOrEmpty(session_id))
        {
            return RedirectToAction(nameof(Index));
        }

        // Get session details from Stripe to complete the purchase
        Stripe.StripeConfiguration.ApiKey = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Stripe:SecretKey"];

        try
        {
            var sessionService = new Stripe.Checkout.SessionService();
            var session = await sessionService.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                var studentId = session.Metadata["student_id"];
                var quantity = int.Parse(session.Metadata["quantity"]);

                // Complete the purchase
                var token = await _tokenService.CompletePurchaseAsync(
                    studentId, quantity, session_id, session.PaymentIntentId);

                if (token != null)
                {
                    TempData["Success"] = $"Successfully purchased {quantity} token(s)! Your new balance is ready to use.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token purchase success for session {SessionId}", session_id);
        }

        return View();
    }

    /// <summary>
    /// Display full transaction history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var userId = GetUserId();
        var transactions = await _tokenService.GetTransactionHistoryAsync(userId);

        var viewModel = transactions.Select(t => new TokenTransactionViewModel
        {
            Id = t.Id,
            Type = t.Type.ToString(),
            Quantity = t.Quantity,
            BalanceAfter = t.BalanceAfter,
            Details = t.Details,
            CreatedAt = t.CreatedAt
        }).ToList();

        return View(viewModel);
    }
}
