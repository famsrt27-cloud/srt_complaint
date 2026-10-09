namespace SRT.Complaint.Services;

public interface IStaffDirectoryService
{
    Task<Dictionary<int, string>> GetNamesByIdsAsync(IEnumerable<int> staffIds);
}
