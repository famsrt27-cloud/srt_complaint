using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Data;

public class CorruptionDbContext(DbContextOptions<CorruptionDbContext> options) : DbContext(options)
{
    public DbSet<CorruptionReport> Reports => Set<CorruptionReport>();
    public DbSet<InvestigationLog> InvestigationLogs => Set<InvestigationLog>();
    public DbSet<DecryptionLog> DecryptionLogs => Set<DecryptionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CorruptionReport>(e =>
        {
            e.ToTable("Reports", "corruption");
            e.HasIndex(x => x.ReferenceNumber).IsUnique();
            e.Property(x => x.ReferenceNumber).HasMaxLength(20);
            e.Property(x => x.ReporterNameMasked).HasMaxLength(200);
            e.Property(x => x.ReporterPhoneMasked).HasMaxLength(20);
            e.Property(x => x.ReporterEmailMasked).HasMaxLength(200);
            e.Property(x => x.SubjectType).HasMaxLength(100);
            e.Property(x => x.SubjectPersonName).HasMaxLength(200);
            e.Property(x => x.SubjectDepartment).HasMaxLength(200);
            e.Property(x => x.Priority).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<InvestigationLog>(e =>
        {
            e.ToTable("InvestigationLogs", "corruption");
            e.HasOne(x => x.Report).WithMany(r => r.InvestigationLogs).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
            // Author references dbo.StaffUsers — no FK enforced at EF level across contexts
            e.Ignore(x => x.Author);
        });

        modelBuilder.Entity<DecryptionLog>(e =>
        {
            e.ToTable("DecryptionLogs", "corruption");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.HasOne(x => x.Report).WithMany(r => r.DecryptionLogs).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.RequestedBy);
        });
    }
}
