using Microsoft.EntityFrameworkCore;
using SAloha.Api.Core.Audit;
using SAloha.Api.Core.Links;
using SAloha.Api.Core.Search;
using SAloha.Api.Modules.ChangeEnablement;
using SAloha.Api.Modules.ComplianceAssessment;
using SAloha.Api.Modules.ContinualImprovement;
using SAloha.Api.Modules.IncidentManagement;
using SAloha.Api.Modules.KnowledgeManagement;
using SAloha.Api.Modules.ProblemManagement;
using SAloha.Api.Modules.ServiceConfigurationManagement;
using SAloha.Api.Modules.ServiceLevelManagement;
using SAloha.Api.Modules.ServiceRequestManagement;

namespace SAloha.Api.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Socle
    public DbSet<ItemLink> ItemLinks => Set<ItemLink>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SearchPassage> SearchPassages => Set<SearchPassage>();
    // Gestion des problèmes
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<RcaAnalysis> Analyses => Set<RcaAnalysis>();
    public DbSet<CorrectiveAction> Actions => Set<CorrectiveAction>();
    public DbSet<RaciAssignment> RaciAssignments => Set<RaciAssignment>();
    // Gestion des incidents, des demandes, habilitation des changements
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Change> Changes => Set<Change>();
    // Configuration des services
    public DbSet<ConfigurationItem> ConfigurationItems => Set<ConfigurationItem>();
    public DbSet<CiRelationship> CiRelationships => Set<CiRelationship>();
    // Niveaux de service
    public DbSet<ItService> Services => Set<ItService>();
    public DbSet<ServiceLevelAgreement> Agreements => Set<ServiceLevelAgreement>();
    // Connaissances, amélioration continue
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<Improvement> Improvements => Set<Improvement>();
    // Conformité NIS 2
    public DbSet<SecurityObjective> SecurityObjectives => Set<SecurityObjective>();
    public DbSet<SecurityRequirement> SecurityRequirements => Set<SecurityRequirement>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentResponse> AssessmentResponses => Set<AssessmentResponse>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Problem>().HasIndex(p => p.Reference).IsUnique();
        b.Entity<Problem>().HasMany(p => p.Analyses).WithOne(a => a.Problem!)
            .HasForeignKey(a => a.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Problem>().HasMany(p => p.Actions).WithOne(a => a.Problem!)
            .HasForeignKey(a => a.ProblemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CorrectiveAction>().HasMany(a => a.Raci).WithOne(r => r.Action!)
            .HasForeignKey(r => r.CorrectiveActionId).OnDelete(DeleteBehavior.Cascade);

        // Référence unique : c'est l'index qui départage deux créations simultanées.
        b.Entity<Incident>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ServiceRequest>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<Change>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ConfigurationItem>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ItService>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ServiceLevelAgreement>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<KnowledgeArticle>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<Improvement>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<Assessment>().HasIndex(x => x.Reference).IsUnique();

        // Le numéro de l'objectif (1 à 20) est son identifiant : pas de séquence.
        b.Entity<SecurityObjective>().Property(o => o.Id).ValueGeneratedNever();
        b.Entity<SecurityObjective>().HasMany(o => o.Requirements).WithOne(r => r.Objective!)
            .HasForeignKey(r => r.ObjectiveId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SecurityRequirement>().HasIndex(r => r.Code).IsUnique();
        b.Entity<Assessment>().HasMany(a => a.Responses).WithOne(r => r.Assessment!)
            .HasForeignKey(r => r.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AssessmentResponse>().HasOne(r => r.Requirement).WithMany()
            .HasForeignKey(r => r.RequirementId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AssessmentResponse>().HasIndex(r => new { r.AssessmentId, r.RequirementId }).IsUnique();

        b.Entity<ConfigurationItem>().HasMany(c => c.Outgoing).WithOne(r => r.Source!)
            .HasForeignKey(r => r.SourceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ConfigurationItem>().HasMany(c => c.Incoming).WithOne(r => r.Target!)
            .HasForeignKey(r => r.TargetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ItService>().HasMany(s => s.Agreements).WithOne(a => a.Service!)
            .HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ServiceLevelAgreement>().Property(a => a.AvailabilityTarget).HasPrecision(5, 2);

        // Index de recherche (ADR-0013) : plein texte « french » généré par
        // PostgreSQL sur le titre et le passage, index GIN.
        b.Entity<SearchPassage>()
            .HasGeneratedTsVectorColumn(p => p.SearchVector, "french", p => new { p.Title, p.Text })
            .HasIndex(p => p.SearchVector).HasMethod("GIN");
        b.Entity<SearchPassage>().HasIndex(p => new { p.SourceType, p.SourceId });

        b.Entity<ItemLink>().HasIndex(l => new { l.FromType, l.FromId });
        b.Entity<ItemLink>().HasIndex(l => new { l.ToType, l.ToId });
        b.Entity<AuditEntry>().HasIndex(a => new { a.EntityType, a.EntityId });
        b.Entity<AuditEntry>().HasIndex(a => a.At);
    }
}
