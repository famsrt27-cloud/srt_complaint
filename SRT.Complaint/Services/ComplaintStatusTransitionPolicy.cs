using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public static class ComplaintStatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedByWorkflow =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [ComplaintWorkflowSteps.Received] = Set("Pending", "Rejected"),
            [ComplaintWorkflowSteps.Screening] = Set("InProgress", "WaitingInfo", "Rejected"),
            [ComplaintWorkflowSteps.WaitingInfo] = Set("WaitingInfo", "InProgress", "Rejected"),
            [ComplaintWorkflowSteps.Classification] = Set("InProgress", "Forwarded"),
            [ComplaintWorkflowSteps.Investigation] = Set("InProgress", "Forwarded"),
            [ComplaintWorkflowSteps.Summary] = Set("Resolved"),
            [ComplaintWorkflowSteps.Notification] = Set("Resolved", "Closed"),
            [ComplaintWorkflowSteps.NotificationRetry] = Set("Resolved", "Closed"),
            [ComplaintWorkflowSteps.Completed] = Set("Closed"),
            [ComplaintWorkflowSteps.Rejected] = Set("Rejected")
        };

    public static bool CanSetStatus(string? workflowStep, string newStatus) =>
        workflowStep != null &&
        AllowedByWorkflow.TryGetValue(workflowStep, out var allowed) &&
        allowed.Contains(newStatus);

    public static bool RequiresNote(string newStatus) =>
        newStatus is "WaitingInfo" or "Forwarded" or "Closed" or "Rejected";

    public static string NextWorkflowStep(string currentWorkflowStep, string newStatus) => newStatus switch
    {
        "WaitingInfo" => ComplaintWorkflowSteps.WaitingInfo,
        "InProgress" when currentWorkflowStep == ComplaintWorkflowSteps.WaitingInfo => ComplaintWorkflowSteps.Screening,
        "Closed" => ComplaintWorkflowSteps.Completed,
        "Rejected" => ComplaintWorkflowSteps.Rejected,
        _ => currentWorkflowStep
    };

    public static string StatusForWorkflow(string workflowStep) => workflowStep switch
    {
        ComplaintWorkflowSteps.Received => "Pending",
        ComplaintWorkflowSteps.WaitingInfo => "WaitingInfo",
        ComplaintWorkflowSteps.Summary or
        ComplaintWorkflowSteps.Notification or
        ComplaintWorkflowSteps.NotificationRetry => "Resolved",
        ComplaintWorkflowSteps.Completed => "Closed",
        ComplaintWorkflowSteps.Rejected => "Rejected",
        _ => "InProgress"
    };

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
