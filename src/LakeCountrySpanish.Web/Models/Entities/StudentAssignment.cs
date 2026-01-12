namespace LakeCountrySpanish.Web.Models.Entities;

/// <summary>
/// Status of an assignment in a student's queue.
/// </summary>
public enum StudentAssignmentStatus
{
    Assigned,
    InProgress,
    Completed,
    Skipped,
    Expired
}

/// <summary>
/// Represents an assignment in a student's queue.
/// This tracks which assignments have been assigned to which students.
/// </summary>
public class StudentAssignment
{
    public int Id { get; set; }

    /// <summary>
    /// The student this assignment is assigned to
    /// </summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// The assignment
    /// </summary>
    public int AssignmentId { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public StudentAssignmentStatus Status { get; set; } = StudentAssignmentStatus.Assigned;

    /// <summary>
    /// When the assignment was assigned
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional due date
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// When the student started working on it
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the student completed it
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Who assigned this (null if auto-assigned)
    /// </summary>
    public string? AssignedById { get; set; }

    /// <summary>
    /// Optional notes from the assigner
    /// </summary>
    public string? AssignerNotes { get; set; }

    /// <summary>
    /// Whether this was auto-assigned based on curriculum progress
    /// </summary>
    public bool IsAutoAssigned { get; set; }

    /// <summary>
    /// Priority for display ordering (lower = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Number of times the student has attempted this assignment
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Best score achieved (if multiple attempts allowed)
    /// </summary>
    public decimal? BestScore { get; set; }

    // Navigation properties
    public virtual ApplicationUser Student { get; set; } = null!;
    public virtual Assignment Assignment { get; set; } = null!;
    public virtual ApplicationUser? AssignedBy { get; set; }
}
