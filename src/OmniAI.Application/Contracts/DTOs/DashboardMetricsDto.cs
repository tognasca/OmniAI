using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record DashboardMetricsDto(
    int TotalMessagesToday, int TotalMessagesPeriod,
    double AiResolutionRate, double AvgResponseTimeSeconds,
    int PendingHandoffs, decimal TotalCostUsd,
    IEnumerable<DailyVolumeDto> DailyVolume);
