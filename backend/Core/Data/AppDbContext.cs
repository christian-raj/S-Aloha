using Microsoft.EntityFrameworkCore;
using SAloha.Api.Modules.ProblemManagement;

namespace SAloha.Api.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<RcaAnalysis> Analyses => Set<RcaAnalysis>();
    public DbSet<CorrectiveAction> Actions => Set<CorrectiveAction>();
    public DbSet<RaciAssignment> RaciAssignments => Set<RaciAssignment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Problem>().HasIndex(p => p.Reference).IsUnique();
        b.Entity<Problem>().HasMany(p => p.Analyses).WithOne(a => a.Problem!)
            .HasForeignKey(a => a.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Problem>().HasMany(p => p.Actions).WithOne(a => a.Problem!)
            .HasForeignKey(a => a.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CorrectiveAction>().HasMany(a => a.Raci).WithOne(r => r.Action!)
            .HasForeignKey(r => r.CorrectiveActionId).OnDelete(DeleteBehavior.Cascade);
    }
}
