namespace OmniAI.Domain.Enums;

public enum TenantPlan    { Free, Basic, Pro, Enterprise }
public enum NumberStatus  { Disconnected, Connecting, Online, Banned, Suspended }

public enum ConversationStatus
{
    Open,         // IA respondendo
    WaitingHuman, // aguardando agente
    InProgress,   // agente assumiu
    Resolved,
    Abandoned
}

public enum HandoffReason
{
    None, KeywordTrigger, IntentDetected,
    AgentRequested, SlaBreached, ErrorFallback
}

public enum MessageDirection { Inbound, Outbound }

public enum MessageType
{
    Text, Image, Audio, Video,
    Document, Location, Sticker,
    Interactive, Template, Reaction
}

public enum SenderType     { User, Bot, Agent }
public enum TriggerType    { Keyword, IntentPattern, Regex }
public enum TriggerPriority { Low, Medium, High, Critical }

public enum SourceType
{
    ManualText, PdfUpload, DocxUpload, UrlCrawl, ApiSync
}

public enum SourceStatus
{
    Pending, Processing, Indexed, Failed, Outdated
}
