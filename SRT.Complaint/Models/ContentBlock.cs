#nullable enable
using System.ComponentModel.DataAnnotations;

namespace SRT.Complaint.Models;

public class ContentBlock
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Key { get; set; } = "";

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public string BodyHtml { get; set; } = "";

    /// <summary>Structured form data (JSON) backing the admin editor, so re-editing doesn't require re-parsing BodyHtml.</summary>
    public string? BodyJson { get; set; }

    [MaxLength(500)]
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? UpdatedById { get; set; }
    public StaffUser? UpdatedBy { get; set; }
}
