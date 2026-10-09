namespace SRT.Complaint.Services;

public interface ISlaService
{
    Task<DateTime> CalculateDeadlineAsync(string priority, DateTime from);
}
