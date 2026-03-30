//using OmniAI.Domain.Entities;
//using OmniAI.Domain.Enums;

//namespace OmniAI.Infrastructure.Persistence;

//public static class OmniDbSeeder
//{
//    public static async Task SeedAsync(OmniDbContext db)
//    {
//        if (db.Tenants.Any()) return;

//        var tenant = new Tenant
//        {
//            Id = Guid.NewGuid(),
//            Name = "Demo Corp",
//            Slug = "demo-corp",
//            Plan = TenantPlan.Pro,
//            IsActive = true,
//        };
//        db.Tenants.Add(tenant);

//        var number = new WhatsAppNumber
//        {
//            Id = Guid.NewGuid(),
//            TenantId = tenant.Id,
//            PhoneNumberId = "REPLACE_WITH_PHONE_NUMBER_ID",
//            WabaId = "REPLACE_WITH_WABA_ID",
//            DisplayName = "Suporte OmniAI",
//            AccessToken = "REPLACE_WITH_ACCESS_TOKEN",
//            VerifyToken = "omni_verify_demo_2026",
//            Status = NumberStatus.Disconnected,
//        };
//        db.WhatsAppNumbers.Add(number);

//        db.BotConfigs.Add(new BotConfig
//        {
//            Id = Guid.NewGuid(),
//            WhatsAppNumberId = number.Id,
//            SystemPrompt = "Você é Sofia, assistente virtual da Demo Corp. Seja sempre cordial, " +
//                           "profissional e objetivo. Responda em português brasileiro. " +
//                           "Foque em ajudar o cliente da melhor forma possível.",
//            AiModel = "gpt-4o",
//            Temperature = 0.3f,
//            MaxTokens = 1000,
//            IsActive = true,
//            WorkStart = TimeOnly.Parse("08:00"),
//            WorkEnd = TimeOnly.Parse("18:00"),
//            OutOfHoursMsg = "Olá! Nosso atendimento funciona das 8h às 18h. " +
//                            "Deixe sua mensagem que retornaremos em breve! 😊",
//            ContextMessages = 10,
//        });

//        var defaultTriggers = new[]
//        {
//            ("atendente", TriggerPriority.High),
//            ("humano", TriggerPriority.High),
//            ("cancelar", TriggerPriority.High),
//            ("reclamação", TriggerPriority.High),
//            ("falar com alguém", TriggerPriority.Critical),
//            ("quero cancelar", TriggerPriority.Critical),
//            ("problema grave", TriggerPriority.Critical),
//        };

//        db.HandoffTriggers.AddRange(defaultTriggers.Select(t => new HandoffTrigger
//        {
//            Id = Guid.NewGuid(),
//            WhatsAppNumberId = number.Id,
//            TriggerType = TriggerType.Keyword,
//            Value = t.Item1,
//            Priority = t.Item2,
//            IsActive = true,
//            TransitionMsg = "Entendido! Estou transferindo você para um especialista. Aguarde um momento. ✅"
//        }));

//        await db.SaveChangesAsync();
//    }
//}]



// Adicione ao OmniDbSeeder.cs — método SeedAuthAsync chamado após SeedAsync

using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;


namespace OmniAI.Infrastructure.Persistence;

public static partial class OmniDbSeeder
{
    public static async Task SeedAuthAsync(OmniDbContext db)
    {
        if (db.AppUsers.Any()) return;

        var tenant = db.Tenants.FirstOrDefault();

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Tenant Padrão",
                Slug = "default",
                Plan = TenantPlan.Free,
                IsActive = true
            };

            db.Tenants.Add(tenant);
            db.SaveChanges();
        }

        // SuperAdmin — acesso total, gerencia todos os tenants
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "superadmin@omni.ai",
            Name = "Super Admin",
            PasswordHash = AppUser.HashPassword("Admin@123"),
            Role = "SuperAdmin",
            IsActive = true,
        });

        // Admin do tenant demo
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "admin@democorp.com",
            Name = "Admin Demo Corp",
            PasswordHash = AppUser.HashPassword("Admin@123"),
            Role = "Admin",
            IsActive = true,
        });

        // Agente (só vê conversas)
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = "agente@democorp.com",
            Name = "Agente Teste",
            PasswordHash = AppUser.HashPassword("Agente@123"),
            Role = "Agent",
            IsActive = true,
        });

        await db.SaveChangesAsync();
    }
}
