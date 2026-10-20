using Microsoft.EntityFrameworkCore;
using RecordService.DataAccess.Entities;
using RecordService.DTOs.DatabaseSuggestionDto;
using RecordService.DTOs.FeedbackDto;
using RecordService.DTOs.UserDto;

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
    public DbSet<DigestOutboxEntity> DigestOutbox => Set<DigestOutboxEntity>();
    public DbSet<SourceRecordEntity> SourceRecords => Set<SourceRecordEntity>();
    public DbSet<SearchQueryRecordEntity> SearchQueryRecords => Set<SearchQueryRecordEntity>();
    public DbSet<FeedbackEntity> Feedback => Set<FeedbackEntity>();
    public DbSet<DatabaseSuggestionEntity> DatabaseSuggestions => Set<DatabaseSuggestionEntity>();
    public DbSet<DatabaseSuggestionVoteEntity> DatabaseSuggestionVotes => Set<DatabaseSuggestionVoteEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(UserFieldLimits.Name);
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(UserFieldLimits.Username);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(UserFieldLimits.Email);
            entity.Property(e => e.KeycloakSub).HasColumnName("keycloak_sub");
            entity.Property(e => e.DigestsStopped).HasColumnName("digests_stopped").HasDefaultValue(false);
            entity.Property(e => e.IsMarkedForDeletion).HasColumnName("is_marked_for_deletion").HasDefaultValue(false);
            entity.Property(e => e.DeletionRequestedAt).HasColumnName("deletion_requested_at");
            entity.Property(e => e.LastActiveAt).HasColumnName("last_active_at").HasDefaultValueSql("now()");
            entity.Property(e => e.InactivityNoticeSentAt).HasColumnName("inactivity_notice_sent_at");
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
            // Wider than the 2048 input limit: PubMed normalization can rewrite the stored URL.
            entity.Property(e => e.TargetUrl).HasColumnName("target_url").HasMaxLength(4096);
            entity.Property(e => e.LastDigestSentAt).HasColumnName("last_digest_sent_at");
            entity.Property(e => e.LastPolledAt).HasColumnName("last_polled_at");
            entity.Property(e => e.SourceRecordCount).HasColumnName("source_record_count");
            entity.Property(e => e.LastPollFailedAt).HasColumnName("last_poll_failed_at");
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
            entity.Property(e => e.Abstract).HasColumnName("abstract");
            entity.Property(e => e.SourceUrl).HasColumnName("source_url");
            entity.Property(e => e.AuthorIntention).HasColumnName("author_intention");
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

        modelBuilder.Entity<DigestOutboxEntity>(entity =>
        {
            entity.ToTable("digest_outbox");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Subject).HasColumnName("subject");
            entity.Property(e => e.HtmlBody).HasColumnName("html_body");
            entity.Property(e => e.ClaimsJson).HasColumnName("claims").HasColumnType("jsonb");
            entity.Property(e => e.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            entity.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(e => e.SentAt).HasColumnName("sent_at");
            entity.Property(e => e.FailedAt).HasColumnName("failed_at");
            entity.Property(e => e.LastError).HasColumnName("last_error");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.NextAttemptAt).HasFilter("sent_at IS NULL AND failed_at IS NULL");
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

        // Feedback and suggestions outlive the submitter: purging a user nulls the author, votes go with the user.
        modelBuilder.Entity<FeedbackEntity>(entity =>
        {
            entity.ToTable("feedback");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(FeedbackFieldLimits.Type);
            entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(FeedbackFieldLimits.Message);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DatabaseSuggestionEntity>(entity =>
        {
            entity.ToTable("database_suggestions");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(DatabaseSuggestionFieldLimits.Name);
            entity.Property(e => e.WebsiteUrl).HasColumnName("website_url").HasMaxLength(DatabaseSuggestionFieldLimits.WebsiteUrl);
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(DatabaseSuggestionFieldLimits.Reason);
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DatabaseSuggestionVoteEntity>(entity =>
        {
            entity.ToTable("database_suggestion_votes");
            entity.HasKey(e => new { e.SuggestionId, e.UserId });
            entity.Property(e => e.SuggestionId).HasColumnName("suggestion_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne<DatabaseSuggestionEntity>().WithMany().HasForeignKey(e => e.SuggestionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserEntity>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
