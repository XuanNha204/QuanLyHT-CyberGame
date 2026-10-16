namespace CyberGame.Application.DTOs;

public record DashboardSummaryDto(
    int TotalMachines,
    int AvailableMachines,
    int InUseMachines,
    int MaintenanceMachines,
    int OfflineMachines,
    decimal TodayRevenue,
    int TodayOrdersCount,
    int ActiveSessionsCount,
    int LowStockCount,
    ShiftDto? CurrentShift,
    IReadOnlyList<MachineDto> Machines
);

public record RevenueReportDto(
    DateOnly Date,
    decimal MachineRevenue,
    decimal ServiceRevenue,
    decimal TotalRevenue,
    int OrderCount,
    int SessionCount
);

public record MachineUtilizationDto(
    Guid MachineId,
    string MachineCode,
    string DisplayName,
    string GroupName,
    int TotalSessions,
    decimal TotalUsageHours,
    decimal TotalRevenue
);

public record TopProductReportDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string CategoryName,
    decimal QuantitySold,
    decimal TotalRevenue
);

public record WalletLiabilityReportDto(
    int TotalCustomersWithBalance,
    decimal TotalLiabilityAmount
);
