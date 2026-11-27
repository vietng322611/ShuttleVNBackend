using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.DTOs.Statistics;
using ShuttleVNBackend.Application.UseCases.Statistics.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Get(CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await dashboardService.GetTodayAsync(ct)));
    }
}