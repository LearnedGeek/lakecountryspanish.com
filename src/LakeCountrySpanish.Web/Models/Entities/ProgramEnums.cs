namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// How a parent chose to pay for a <see cref="ProgramEnrollment"/>.
/// Determines which Stripe flow (if any) runs at enrollment time.
/// </summary>
public enum ProgramPaymentType
{
    /// <summary>Single Stripe Checkout at full price.</summary>
    FullOneTime = 0,

    /// <summary>
    /// Stripe subscription against the program's installment price, with
    /// <c>cancel_at</c> set past the final installment date so Stripe auto-charges
    /// the remaining installments and then cancels itself.
    /// </summary>
    TwoInstallment = 1,

    /// <summary>
    /// Parent chose to pay Karen directly at the booth (cash or check). No
    /// Stripe transaction. Enrollment is recorded and Karen reconciles manually.
    /// </summary>
    CashInHand = 2
}

/// <summary>
/// Payment-state machine for a <see cref="ProgramEnrollment"/>. Transitions
/// happen on Stripe webhooks (payment paths) or admin action (cash / refund /
/// cancel). Purely a payment concept — separate from whether the student is
/// enrolled in the class (which is implicit: an enrollment row exists).
/// </summary>
public enum ProgramEnrollmentStatus
{
    /// <summary>Form was submitted; awaiting first Stripe payment to land.</summary>
    PendingPayment = 0,

    /// <summary>Installment 1 charged successfully; installment 2 still due.</summary>
    FirstPaymentComplete = 1,

    /// <summary>All expected payments received (full one-time OR final installment).</summary>
    FullyPaid = 2,

    /// <summary>
    /// Parent selected cash-in-hand; Karen hasn't confirmed receipt yet.
    /// Karen flips this to <see cref="FullyPaid"/> via admin once she has the cash.
    /// </summary>
    CashPending = 3,

    /// <summary>Enrollment cancelled — no charge, or Stripe payment failed and won't retry.</summary>
    Cancelled = 4,

    /// <summary>Charge was refunded (fully or partially) after being received.</summary>
    Refunded = 5
}
