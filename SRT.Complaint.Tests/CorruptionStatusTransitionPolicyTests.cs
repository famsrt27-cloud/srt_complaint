using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class CorruptionStatusTransitionPolicyTests
{
    [Theory]
    [InlineData("Pending", "InProgress")]
    [InlineData("InProgress", "UnderReview")]
    [InlineData("InProgress", "Rejected")]
    [InlineData("UnderReview", "InProgress")]
    [InlineData("UnderReview", "Closed")]
    [InlineData("UnderReview", "Rejected")]
    [InlineData("Closed", "InProgress")]
    public void CanTransition_AllowsApprovedWorkflow(string current, string next)
    {
        Assert.True(CorruptionStatusTransitionPolicy.CanTransition(current, next));
    }

    [Theory]
    [InlineData("Pending", "Closed")]
    [InlineData("Pending", "UnderReview")]
    [InlineData("InProgress", "Closed")]
    [InlineData("Closed", "Rejected")]
    [InlineData("Rejected", "InProgress")]
    [InlineData("Unknown", "Pending")]
    public void CanTransition_RejectsSkippedOrTerminalTransitions(string current, string next)
    {
        Assert.False(CorruptionStatusTransitionPolicy.CanTransition(current, next));
    }
}
