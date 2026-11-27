using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.DTOs.Statistics;
using ShuttleVNBackend.Application.UseCases.Statistics.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/admin/statistics")]
[Authorize(Policy = "AdminOnly")]
public class StatisticsController(StatisticsService statisticsService) : ControllerBase
{
    [HttpGet("court-usage")]
    public async Task<ActionResult<ApiResponse<CourtUsageSummaryDto>>> GetCourtUsage(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] RevenueGroupBy groupBy = RevenueGroupBy.Day,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(
            await statisticsService.GetCourtUsageAsync(fromDate, toDate, groupBy, ct)));
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<RevenueSummaryDto>>> GetRevenue(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] RevenueGroupBy groupBy = RevenueGroupBy.Day,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(
            await statisticsService.GetRevenueAsync(fromDate, toDate, groupBy, ct)));
    }
}