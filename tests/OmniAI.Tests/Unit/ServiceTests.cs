using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;
using OmniAI.Domain.Interfaces;
using OmniAI.Infrastructure.Persistence;
using OmniAI.Infrastructure.Services;
using Xunit;

namespace OmniAI.Tests.Unit;

// ── Helpers ───────────────────────────────────────────────────────

public static class TestDbFactory
{
    public static OmniDbContext Create(string dbName)
    {
        var opts = new DbContextOptionsBuilder<OmniDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new OmniDbContext(opts);
    }
}

// ── Handoff Service Tests ─────────────────────────────────────────

public class HandoffServiceTests
{
    [Theory]
    [InlineData("quero falar com um atendente", true)]
    [InlineData("quero cancelar meu plano", true)]
    [InlineData("qual o horário de funcionamento?", false)]
    [InlineData("ATENDENTE por favor", true)]   // case-insensitive
    public async Task CheckTriggers_ShouldMatch_WhenKeywordPresent(string message, bool expected)
    {
        // Arrange
        var db = TestDbFactory.Create($"handoff-{Guid.NewGuid()}");
        var numberId = Guid.NewGuid();

        db.HandoffTriggers.AddRange(
            new HandoffTrigger { Id = Guid.NewGuid(), WhatsAppNumberId = numberId, TriggerType = TriggerType.Keyword, Value = "atendente", Priority = TriggerPriority.High, IsActive = true },
            new HandoffTrigger { Id = Guid.NewGuid(), WhatsAppNumberId = numberId, TriggerType = TriggerType.Keyword, Value = "cancelar", Priority = TriggerPriority.High, IsActive = true }
        );
        await db.SaveChangesAsync();

        var svc = new HandoffService(db,
            Mock.Of<IWhatsAppService>(),
            Mock.Of<IDashboardNotifier>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<HandoffService>>());

        // Act
        var result = await svc.CheckTriggersAsync(numberId, message);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CheckTriggers_ShouldNotMatch_WhenTriggersDisabled()
    {
        var db = TestDbFactory.Create($"handoff-disabled-{Guid.NewGuid()}");
        var numberId = Guid.NewGuid();

        db.HandoffTriggers.Add(new HandoffTrigger
        {
            Id = Guid.NewGuid(), WhatsAppNumberId = numberId,
            TriggerType = TriggerType.Keyword, Value = "cancelar",
            Priority = TriggerPriority.High, IsActive = false  // disabled
        });
        await db.SaveChangesAsync();

        var svc = new HandoffService(db,
            Mock.Of<IWhatsAppService>(),
            Mock.Of<IDashboardNotifier>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<HandoffService>>());

        var result = await svc.CheckTriggersAsync(numberId, "quero cancelar");
        result.Should().BeFalse();
    }
}

// ── Conversation Status Tests ─────────────────────────────────────

public class ConversationStatusTests
{
    [Fact]
    public async Task TriggerHandoff_ShouldSetStatusToWaitingHuman()
    {
        var db = TestDbFactory.Create($"conv-{Guid.NewGuid()}");
        var tenantId  = Guid.NewGuid();
        var numberId  = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Seed minimal data
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", Plan = TenantPlan.Pro });
        db.WhatsAppNumbers.Add(new WhatsAppNumber
        {
            Id = numberId, TenantId = tenantId,
            PhoneNumberId = "pid", WabaId = "wid",
            DisplayName = "N", AccessToken = "tok", VerifyToken = "vt"
        });
        var contact = new Contact
        {
            Id = contactId, TenantId = tenantId,
            PhoneNumber = "+5511999999999", FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);

        var conv = new Conversation
        {
            Id = Guid.NewGuid(), ContactId = contactId,
            WhatsAppNumberId = numberId, Status = ConversationStatus.Open,
            StartedAt = DateTime.UtcNow
        };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // Mock WhatsApp to avoid real HTTP
        var waMock = new Mock<IWhatsAppService>();
        waMock.Setup(w => w.SendTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var svc = new HandoffService(db, waMock.Object,
            Mock.Of<IDashboardNotifier>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<HandoffService>>());

        await svc.TriggerHandoffAsync(conv.Id, "keyword");

        var updated = await db.Conversations.FindAsync(conv.Id);
        updated!.Status.Should().Be(ConversationStatus.WaitingHuman);
        updated.HandoffReason.Should().Be(HandoffReason.KeywordTrigger);
    }
}

// ── Knowledge Chunking Tests ──────────────────────────────────────

public class KnowledgeChunkingTests
{
    [Fact]
    public void ChunkText_ShouldProduceCorrectNumberOfChunks()
    {
        // Generate a text of ~5000 chars (> 1 chunk)
        var text = string.Join(" ", Enumerable.Repeat("palavra", 1000));

        // Access internal method via reflection for white-box testing
        var method = typeof(OmniAI.Infrastructure.Services.KnowledgeIndexing.KnowledgeIndexingService)
            .GetMethod("SplitIntoChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var chunks = (List<string>?)method?.Invoke(null, [text, 500, 50]);
        chunks.Should().NotBeNull();
        chunks!.Count.Should().BeGreaterThan(1);
        chunks.All(c => c.Length > 20).Should().BeTrue();
    }

    [Fact]
    public void CosineSimilarity_ShouldReturnOneForIdenticalVectors()
    {
        var method = typeof(OmniAI.Infrastructure.Services.KnowledgeIndexing.KnowledgeIndexingService)
            .GetMethod("CosineSimilarity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        float[] vec = [1f, 0f, 0f, 1f];
        var result = (float?)method?.Invoke(null, [vec, vec]);
        result.Should().BeApproximately(1f, 0.001f);
    }
}
