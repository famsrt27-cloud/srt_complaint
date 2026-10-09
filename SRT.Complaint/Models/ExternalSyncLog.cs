#nullable enable
namespace SRT.Complaint.Models;

public class ExternalSyncLog
{
    public int      Id              { get; set; }
    public string   ExternalSystem  { get; set; } = string.Empty;
    public DateTime StartedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt    { get; set; }
    public int      FetchedCount    { get; set; }
    public int      NewCount        { get; set; }
    public int      DuplicateCount  { get; set; }
    public int      ErrorCount      { get; set; }
    public string   SyncStatus      { get; set; } = "Running"; // Running | Success | Failed
    public string?  ErrorMessage    { get; set; }
    public int?     TriggeredById   { get; set; } // null = triggered by the scheduled background sync, not a staff member

    public StaffUser? TriggeredBy { get; set; }
}
