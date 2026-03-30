using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;
using OmniAI.Infrastructure.Encryption;

namespace OmniAI.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.Plan).HasConversion<string>().HasMaxLength(20);
    }
}

public class WhatsAppNumberConfiguration : IEntityTypeConfiguration<WhatsAppNumber>
{
    public void Configure(EntityTypeBuilder<WhatsAppNumber> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PhoneNumberId).HasMaxLength(50).IsRequired();
        b.Property(x => x.WabaId).HasMaxLength(50).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.VerifyToken).HasMaxLength(200).IsRequired();
        b.Property(x => x.AccessToken)
         .HasMaxLength(2000)
         .HasConversion(
             v => AesEncryptor.Encrypt(v),
             v => AesEncryptor.Decrypt(v));
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.PhoneNumberId).IsUnique();
        b.HasOne(x => x.Tenant)
         .WithMany(t => t.Numbers)
         .HasForeignKey(x => x.TenantId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Config)
         .WithOne(c => c.Number)
         .HasForeignKey<BotConfig>(c => c.WhatsAppNumberId);
    }
}

public class BotConfigConfiguration : IEntityTypeConfiguration<BotConfig>
{
    public void Configure(EntityTypeBuilder<BotConfig> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.SystemPrompt).HasMaxLength(8000).IsRequired();
        b.Property(x => x.AiModel).HasMaxLength(50).IsRequired();
        b.Property(x => x.OutOfHoursMsg).HasMaxLength(1000);
    }
}

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.ProfilePicUrl).HasMaxLength(2048);
        b.Property(x => x.Tags).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.TenantId, x.PhoneNumber }).IsUnique();
        b.HasOne(x => x.Tenant)
         .WithMany(t => t.Contacts)
         .HasForeignKey(x => x.TenantId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.HandoffReason).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.AgentName).HasMaxLength(200);
        b.Property(x => x.CsatComment).HasMaxLength(1000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.WhatsAppNumberId, x.Status });
        b.HasIndex(x => new { x.ContactId, x.StartedAt });
        b.HasOne(x => x.Contact)
         .WithMany(c => c.Conversations)
         .HasForeignKey(x => x.ContactId)
         .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Number)
         .WithMany(n => n.Conversations)
         .HasForeignKey(x => x.WhatsAppNumberId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.WaMessageId).HasMaxLength(100);
        b.Property(x => x.Content).HasMaxLength(4096);
        b.Property(x => x.MediaUrl).HasMaxLength(2048);
        b.Property(x => x.MediaMimeType).HasMaxLength(100);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SenderType).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(x => x.WaMessageId).IsUnique()
         .HasFilter("[WaMessageId] IS NOT NULL");
        b.HasIndex(x => new { x.ConversationId, x.SentAt });
        b.HasOne(x => x.Conversation)
         .WithMany(c => c.Messages)
         .HasForeignKey(x => x.ConversationId)
         .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.UsageLog)
         .WithOne(u => u.Message)
         .HasForeignKey<AiUsageLog>(u => u.MessageId);
    }
}

public class KnowledgeSourceConfiguration : IEntityTypeConfiguration<KnowledgeSource>
{
    public void Configure(EntityTypeBuilder<KnowledgeSource> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalUrl).HasMaxLength(2048);
        b.Property(x => x.FilePath).HasMaxLength(1000);
        b.Property(x => x.OriginalContent).HasColumnType("nvarchar(max)");
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Number)
         .WithMany(n => n.Sources)
         .HasForeignKey(x => x.WhatsAppNumberId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        b.Property(x => x.EmbeddingJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.KnowledgeSourceId, x.ChunkIndex });
        b.HasOne(x => x.Source)
         .WithMany(s => s.Chunks)
         .HasForeignKey(x => x.KnowledgeSourceId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HandoffTriggerConfiguration : IEntityTypeConfiguration<HandoffTrigger>
{
    public void Configure(EntityTypeBuilder<HandoffTrigger> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.Property(x => x.TriggerType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(15);
        b.Property(x => x.TransitionMsg).HasMaxLength(1000);
        b.HasIndex(x => new { x.WhatsAppNumberId, x.IsActive });
        b.HasOne(x => x.Number)
         .WithMany(n => n.Triggers)
         .HasForeignKey(x => x.WhatsAppNumberId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Model).HasMaxLength(50).IsRequired();
        b.Property(x => x.CostUsd).HasPrecision(18, 8);
        b.HasIndex(x => new { x.WhatsAppNumberId, x.CreatedAt });
    }
}
