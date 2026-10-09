using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;

namespace SRT.Complaint.Services;

public class SlaService(AppDbContext db) : ISlaService
{
    public async Task<DateTime> CalculateDeadlineAsync(string priority, DateTime from)
    {
        var config = await db.SlaConfigs.FirstOrDefaultAsync(s => s.Priority == priority);
        var hours = config?.ResolutionHours ?? 168;
        return from.AddHours(hours);
    }
}
