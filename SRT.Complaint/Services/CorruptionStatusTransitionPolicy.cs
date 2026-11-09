namespace SRT.Complaint.Services;

public static class CorruptionStatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Allowed =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Pending"] = ["InProgress"],
            ["InProgress"] = ["UnderReview", "Rejected"],
            ["UnderReview"] = ["InProgress", "Closed", "Rejected"],
            ["Closed"] = ["InProgress"],
            ["Rejected"] = []
        };

    public static bool CanTransition(string currentStatus, string newStatus) =>
        Allowed.TryGetValue(currentStatus, out var next) && next.Contains(newStatus);
}
