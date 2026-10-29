using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class IdempotencyService(AppDbContext db) : IIdempotencyService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<IdempotencyBeginResult> TryBeginAsync(
        int apiKeyId, string idempotencyKey, string requestHash, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.ApiIdempotencyRecords
            .FirstOrDefaultAsync(x => x.ApiKeyId == apiKeyId && x.IdempotencyKey == idempotencyKey, ct);

        if (existing != null)
        {
            if (existing.ExpiresAt <= now)
            {
                db.ApiIdempotencyRecords.Remove(existing);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                return ResolveExisting(existing, requestHash, now);
            }
        }

        var record = new ApiIdempotencyRecord
        {
            ApiKeyId = apiKeyId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            State = "Pending",
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.Add(Retention)
        };
        db.ApiIdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
            return new(IdempotencyBeginState.Started, record.Id);
        }
        catch (DbUpdateException)
        {
            db.Entry(record).State = EntityState.Detached;
            existing = await db.ApiIdempotencyRecords
                .AsNoTracking()
                .FirstAsync(x => x.ApiKeyId == apiKeyId && x.IdempotencyKey == idempotencyKey, ct);
            return ResolveExisting(existing, requestHash, now);
        }
    }

    public async Task CompleteAsync(long recordId, int responseStatus, string responseBody, CancellationToken ct = default)
    {
        var record = await db.ApiIdempotencyRecords.FindAsync([recordId], ct)
            ?? throw new InvalidOperationException("Idempotency record not found");
        record.State = "Completed";
        record.ResponseStatus = responseStatus;
        record.ResponseBody = responseBody;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task AbandonAsync(long recordId, CancellationToken ct = default)
    {
        // A failed business write can leave Added/Modified entities in this scoped
        // DbContext. Detach those changes first so cleanup of the idempotency lock
        // cannot accidentally retry the failed write.
        foreach (var entry in db.ChangeTracker.Entries()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                                 !(x.Entity is ApiIdempotencyRecord record && record.Id == recordId))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        var record = await db.ApiIdempotencyRecords.FindAsync([recordId], ct);
        if (record == null) return;
        db.ApiIdempotencyRecords.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    private static IdempotencyBeginResult ResolveExisting(
        ApiIdempotencyRecord existing, string requestHash, DateTime now)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(existing.RequestHash), Convert.FromHexString(requestHash)))
            return new(IdempotencyBeginState.PayloadConflict, existing.Id);

        if (existing.State == "Completed")
            return new(IdempotencyBeginState.Replay, existing.Id, existing.ResponseStatus, existing.ResponseBody);

        return new(IdempotencyBeginState.InProgress, existing.Id);
    }
}
