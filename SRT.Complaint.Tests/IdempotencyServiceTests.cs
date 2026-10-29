using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class IdempotencyServiceTests
{
    [Fact]
    public async Task SameKeyAndPayload_ReplaysCompletedResponse()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ApiKeys.Add(new ApiKey { Id = 7, PartnerId = 1, Name = "key", KeyType = "External", KeyPrefix = "prefix", KeyHash = "hash", CreatedById = 1 });
        await db.SaveChangesAsync();
        var service = new IdempotencyService(db);
        var hash = new string('a', 64);

        var first = await service.TryBeginAsync(7, "request-0001", hash);
        await service.CompleteAsync(first.RecordId, 201, "{\"referenceNumber\":\"SRT-1\"}");
        var replay = await service.TryBeginAsync(7, "request-0001", hash);

        Assert.Equal(IdempotencyBeginState.Started, first.State);
        Assert.Equal(IdempotencyBeginState.Replay, replay.State);
        Assert.Equal(201, replay.ResponseStatus);
        Assert.Contains("SRT-1", replay.ResponseBody);
    }

    [Fact]
    public async Task SameKeyWithDifferentPayload_ReturnsConflict()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ApiKeys.Add(new ApiKey { Id = 8, PartnerId = 1, Name = "key", KeyType = "External", KeyPrefix = "prefix", KeyHash = "hash2", CreatedById = 1 });
        await db.SaveChangesAsync();
        var service = new IdempotencyService(db);

        await service.TryBeginAsync(8, "request-0002", new string('a', 64));
        var conflict = await service.TryBeginAsync(8, "request-0002", new string('b', 64));

        Assert.Equal(IdempotencyBeginState.PayloadConflict, conflict.State);
    }
}
