using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;


public record DailyVolumeDto(DateTime Date, int Inbound, int Outbound, int Handoffs);