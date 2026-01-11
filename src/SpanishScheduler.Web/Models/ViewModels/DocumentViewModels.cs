using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SpanishScheduler.Web.Models.Entities;

namespace SpanishScheduler.Web.Models.ViewModels;

public class UploadDocumentViewModel
{
    [Required]
    [Display(Name = "Document Title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "File")]
    public IFormFile File { get; set; } = null!;

    [Display(Name = "Available to All Students")]
    public bool IsGlobal { get; set; }

    [Display(Name = "Assign to Specific Students")]
    public List<string> SelectedStudentIds { get; set; } = new List<string>();
}

public class AdminDocumentsViewModel
{
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();
}

public class AssignDocumentViewModel
{
    public int DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public List<StudentAssignmentViewModel> Students { get; set; } = new List<StudentAssignmentViewModel>();
}

public class StudentAssignmentViewModel
{
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class StudentDocumentsViewModel
{
    public IEnumerable<Document> Documents { get; set; } = new List<Document>();
}
