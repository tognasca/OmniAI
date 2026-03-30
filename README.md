# OmniAI WhatsApp Manager

SaaS de atendimento automatizado por IA via WhatsApp Business API (Meta Cloud API oficial).

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Runtime | .NET 8 / ASP.NET Core 8 |
| Arquitetura | Clean Architecture + CQRS (MediatR) |
| Banco de dados | SQL Server 2022 + EF Core 8 (Code-First) |
| IA | OpenAI GPT-4o + text-embedding-3-large (RAG) |
| Tempo real | SignalR |
| Mensageria | System.Threading.Channels (swap → RabbitMQ/Redis em produção) |
| Containers | Docker + Docker Compose |
| Testes | xUnit + Moq + FluentAssertions |

## Estrutura

```
OmniAI/
├── src/
│   ├── OmniAI.Domain/           # Entidades, enums, interfaces
│   ├── OmniAI.Application/      # CQRS handlers, DTOs, contratos
│   ├── OmniAI.Infrastructure/   # EF Core, OpenAI, WhatsApp, Workers
│   └── OmniAI.API/              # Controllers, Hubs, Program.cs
└── tests/
    └── OmniAI.Tests/            # Unit + Integration tests
```

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Conta Meta for Developers com WhatsApp Business configurado
- Chave OpenAI API

## Configuração rápida

### 1. Clonar e configurar variáveis

```bash
git clone <seu-repo>
cd OmniAI
cp .env.example .env
# Edite .env com suas chaves reais
```

### 2. Gerar chaves de criptografia

```bash
# AES Key (32 bytes → base64)
openssl rand -base64 32

# AES IV (16 bytes → base64)
openssl rand -base64 16
```

Cole os valores em `.env` → `AES_KEY` e `AES_IV`.

### 3. Subir com Docker Compose

```bash
docker-compose up -d
```

A API estará disponível em `http://localhost:5000`.  
Swagger UI: `http://localhost:5000/swagger`

### 4. Aplicar migrations manualmente (sem Docker)

```bash
cd src/OmniAI.API
dotnet ef database update --project ../OmniAI.Infrastructure
```

### 5. Configurar Webhook no Meta for Developers

1. Acesse [developers.facebook.com](https://developers.facebook.com)
2. No app → WhatsApp → Configuração do webhook
3. URL: `https://SEU_DOMINIO/api/webhook`
4. Verify Token: valor de `WhatsApp:VerifyToken` no `appsettings.json`
5. Assinar: `messages`, `message_deliveries`, `message_reads`

## Variáveis de ambiente

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__Default` | Connection string SQL Server |
| `OpenAI__ApiKey` | Chave da API OpenAI |
| `WhatsApp__VerifyToken` | Token de verificação do webhook Meta |
| `Encryption__Key` | Chave AES-256 base64 (32 bytes) |
| `Encryption__IV` | IV AES base64 (16 bytes) |
| `Auth__Authority` | URL do provedor JWT |
| `Auth__Audience` | Audience do token JWT |

## Fluxo de mensagem

```
WhatsApp User
    │ mensagem
    ▼
Meta Cloud API ──POST──▶ /api/webhook
                              │
                    IncomingMessageQueue
                              │ (background)
                    MessageProcessorWorker
                              │
                    ┌─────────┴──────────┐
                    │                    │
             Handoff check?          RAG search
             (keyword/intent)     (KnowledgeChunks)
                    │                    │
                  SIM                   NÃO → GPT-4o
                    │                    │
              WaitingHuman          AI reply
              + notificação    + log uso tokens
              SignalR Hub           │
                              WhatsApp API
                              (enviar resposta)
```

## API Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/webhook` | Verificação Meta (challenge) |
| POST | `/api/webhook` | Receber mensagens |
| GET | `/api/conversations` | Listar conversas |
| GET | `/api/conversations/{id}/messages` | Mensagens de uma conversa |
| POST | `/api/conversations/{id}/assign` | Atribuir agente humano |
| POST | `/api/conversations/{id}/close` | Encerrar conversa |
| GET | `/api/numbers` | Listar números WhatsApp |
| POST | `/api/numbers` | Adicionar número |
| GET/PUT | `/api/bot-config/{numberId}` | Config da IA |
| GET | `/api/knowledge/{numberId}` | Base de conhecimento |
| POST | `/api/knowledge/text` | Adicionar texto |
| POST | `/api/knowledge/url` | Indexar URL |
| DELETE | `/api/knowledge/{sourceId}` | Remover fonte |
| GET/PUT | `/api/handoff-triggers` | Gatilhos de transferência |
| GET | `/api/metrics/dashboard` | Métricas do dashboard |
| WS | `/hubs/dashboard` | SignalR real-time |

## Executar testes

```bash
dotnet test
```

## Produção

Para produção, substitua:
- `IBackgroundTaskQueue` → RabbitMQ ou Azure Service Bus
- `EmbeddingJson` (SQL Server) → pgvector (PostgreSQL) para busca vetorial nativa
- Secrets → Azure Key Vault com `DefaultAzureCredential`
- Logs → Azure Application Insights ou Seq

## Licença

MIT
