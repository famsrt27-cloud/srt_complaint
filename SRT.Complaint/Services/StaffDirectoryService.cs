using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;

namespace SRT.Complaint.Services;

public class StaffDirectoryService(AppDbContext db) : IStaffDirectoryService
{
    public async Task<Dictionary<int, string>> GetNamesByIdsAsync(IEnumerable<int> staffIds)
    {
        var ids = staffIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, string>();

        return await db.StaffUsers
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);
    }
}
