namespace SRT.Complaint.Services;

public interface IDiscordNotifier
{
    Task NotifyNewComplaintAsync(NewComplaintDiscordPayload payload, CancellationToken ct = default);
}

public record NewComplaintDiscordPayload(
    string ReferenceNumber,
    string CategoryName,
    string Priority,
    string? SubjectStation,
    DateTime CreatedAt
);
