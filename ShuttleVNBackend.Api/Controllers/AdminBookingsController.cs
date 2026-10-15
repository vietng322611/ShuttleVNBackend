using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.UseCases.Bookings.Services;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Policy = "AdminOnly")]
public class AdminBookingsController(AdminBookingService adminBookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingListItemDto>>>> Search(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? courtId,
        [FromQuery] BookingStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] string? keyword,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var criteria = new BookingSearchCriteria(fromDate, toDate, courtId, status, customerId, keyword);
        var page = new PageRequest(pageNumber, pageSize);
        return Ok(ApiResponseFactory.Success(await adminBookingService.SearchAsync(criteria, page, ct)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> GetDetail(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await adminBookingService.GetDetailAsync(id, ct)));
    }
}