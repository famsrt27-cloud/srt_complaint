#nullable enable
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IContentBlockService
{
    Task<Dictionary<string, ContentBlock>> GetHomeBlocksAsync();

    Task SaveStepsAsync(string title, StepsBlockData data, int staffId);
    Task SaveContactAsync(string title, ContactBlockData data, int staffId);
    Task SaveTrustAsync(string title, TrustBlockData data, int staffId);

    StepsBlockData ParseSteps(ContentBlock? block);
    ContactBlockData ParseContact(ContentBlock? block);
    TrustBlockData ParseTrust(ContentBlock? block);
}
