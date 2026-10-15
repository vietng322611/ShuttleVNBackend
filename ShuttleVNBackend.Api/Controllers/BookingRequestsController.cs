using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.UseCases.Bookings.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/booking-requests")]
[Authorize(Policy = "StaffOnly")]
public class BookingRequestsController(BookingRequestService bookingRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingListItemDto>>>> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var page = new PageRequest(pageNumber, pageSize);
        return Ok(ApiResponseFactory.Success(await bookingRequestService.GetPendingAsync(page, ct)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> GetDetail(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await bookingRequestService.GetDetailAsync(id, ct)));
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Confirm(
        [FromRoute] Guid id,
        [FromBody] ConfirmBookingDto dto,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await bookingRequestService.ConfirmAsync(id, dto, ct)));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Reject(
        [FromRoute] Guid id,
        [FromBody] RejectBookingDto dto,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await bookingRequestService.RejectAsync(id, dto, ct)));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ApiResponse<BookingDetailDto>>> Complete(
        [FromRoute] Guid id,
        [FromBody] ConfirmBookingDto? dto,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await bookingRequestService.CompleteAsync(id, dto, ct)));
    }
}