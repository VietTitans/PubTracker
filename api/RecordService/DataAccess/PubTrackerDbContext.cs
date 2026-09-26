using Microsoft.EntityFrameworkCore;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class PubTrackerDbContext : DbContext
{
    public PubTrackerDbContext(DbContextOptions<PubTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<SearchQueryEntity> SearchQueries => Set<SearchQueryEntity>();
    public DbSet<RecordEntity> Records => Set<RecordEntity>();
    public DbSet<UserSearchQueryEntity> UserSearchQueries => Set<UserSearchQueryEntity>();
    public DbSet<UserSearchQueryDigestEntity> UserSearchQueryDigests => Set<UserSearchQueryDigestEntity>();
    public DbSet<SourceRecordEntity> SourceRecords => Set<SourceRecordEntity>();
    public DbSet<SearchQueryRecordEntity> SearchQueryRecords => Set<SearchQueryRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.KeycloakSub).HasColumnName("keycloak_sub");
            entity.Property(e => e.IsMarkedForDeletion).HasColumnName("is_marked_for_deletion").HasDefaultValue(false);
            entity.Property(e => e.DeletionRequestedAt).HasColumnName("deletion_requested_at");
            entity.HasIndex(e => e.KeycloakSub).IsUnique();
        });

        modelBuilder.Entity<SourceEntity>(entity =>
        {
            entity.ToTable("sources");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.BaseUrl).HasColumnName("base_url");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<SearchQueryEntity>(entity =>
        {
            entity.ToTable("search_queries");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.TargetUrl).HasColumnName("target_url");
            entity.Property(e => e.LastDigestSentAt).HasColumnName("last_digest_sent_at");
            entity.Property(e => e.LastPolledAt).HasColumnName("last_polled_at");
            entity.Property(e => e.SourceRecordCount).HasColumnName("source_record_count");
            entity.HasIndex(e => new { e.SourceId, e.TargetUrl }).IsUnique();
            entity.HasOne<SourceEntity>().WithMany().HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RecordEntity>(entity =>
        {
            entity.ToTable("records");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExternalId).HasColumnName("external_id");
            entity.Property(e => e.Doi).HasColumnName("doi");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.SourceUrl).HasColumnName("source_url");
            entity.HasIndex(e => e.ExternalId).IsUnique();
        });

        modelBuilder.Entity<UserSearchQueryEntity>(entity =>
        {
            entity.ToTable("user_search_queries");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SearchQueryId).HasColumnName("search_query_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.UserId, e.SearchQueryId }).IsUnique();
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SearchQueryEntity>().WithMany().HasForeignKey(e => e.SearchQueryId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSearchQueryDigestEntity>(entity =>
        {
            entity.ToTable("user_search_query_digests");
            entity.HasKey(e => new { e.UserId, e.SearchQueryId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SearchQueryId).HasColumnName("search_query_id");
            entity.Property(e => e.LastDigestSentAt).HasColumnName("last_digest_sent_at");
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SearchQueryEntity>().WithMany().HasForeignKey(e => e.SearchQueryId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SourceRecordEntity>(entity =>
        {
            entity.ToTable("source_records");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RecordId).HasColumnName("record_id");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.HasIndex(e => new { e.RecordId, e.SourceId }).IsUnique();
            entity.HasOne<RecordEntity>().WithMany().HasForeignKey(e => e.RecordId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<SourceEntity>().WithMany().HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SearchQueryRecordEntity>(entity =>
        {
            entity.ToTable("search_query_records");
            entity.HasKey(e => new { e.SearchQueryId, e.RecordId });
            entity.Property(e => e.SearchQueryId).HasColumnName("search_query_id");
            entity.Property(e => e.RecordId).HasColumnName("record_id");
            entity.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");
            entity.HasOne<SearchQueryEntity>().WithMany().HasForeignKey(e => e.SearchQueryId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<RecordEntity>().WithMany().HasForeignKey(e => e.RecordId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
