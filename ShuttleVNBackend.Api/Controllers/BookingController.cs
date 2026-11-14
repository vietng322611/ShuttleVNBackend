using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.UseCases.Bookings.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingController(
    BookingService bookingService,
    BookingRescheduleService bookingRescheduleService) : ControllerBase
{
    [HttpGet("price-preview")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PricePreviewResponse>>> GetPricePreview(
        [FromQuery] int courtId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] TimeOnly endTime,
        CancellationToken ct = default)
    {
        var result = await bookingService.GetPricePreviewAsync(
            new PricePreviewDto(courtId, date, startTime, endTime), ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>Creates a booking with confirmed TotalCost and QuoteFingerprint from price-preview.</summary>
    /// <response code="400">Price confirmation is missing or invalid.</response>
    /// <response code="409">A changed price includes the fresh quote in data for explicit confirmation. Other conflicts return null data.</response>
    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PricePreviewResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BookingDto>>> CreateBooking(
        [FromBody] CreateBookingDto dto,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await bookingService.CreateBookingAsync(dto, idempotencyKey, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>Creates a guest booking with confirmed TotalCost and QuoteFingerprint from price-preview.</summary>
    /// <response code="400">Price confirmation is missing or invalid.</response>
    /// <response code="409">A changed price includes the fresh quote in data for explicit confirmation. Other conflicts return null data.</response>
    [HttpPost("quick")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PricePreviewResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BookingDto>>> QuickBooking(
        [FromBody] QuickBookingDto dto,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await bookingService.QuickBookingAsync(dto, idempotencyKey, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{bookingId:guid}/reschedule-preview")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ReschedulePreviewDto>>> ReschedulePreview(
        [FromRoute] Guid bookingId,
        [FromQuery] int courtId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] TimeOnly endTime,
        CancellationToken ct = default)
    {
        var result = await bookingRescheduleService.PreviewAsync(
            bookingId, new PricePreviewDto(courtId, date, startTime, endTime), ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <response code="409">Slot, original state or quote changed. Reload; never repeat an external refund.</response>
    [HttpPatch("{bookingId:guid}/reschedule")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BookingDto>>> Reschedule(
        [FromRoute] Guid bookingId,
        [FromBody] RescheduleBookingDto dto,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await bookingRescheduleService.RescheduleAsync(bookingId, dto, idempotencyKey, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <response code="409">Booking or invoice changed. Reload; never repeat an external refund.</response>
    [HttpPatch("{bookingId:guid}/cancel")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BookingDto>>> CancelBooking(
        [FromRoute] Guid bookingId,
        [FromBody] CancelBookingDto dto,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await bookingService.CancelBookingAsync(bookingId, dto, idempotencyKey, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("me")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingDto>>>> GetMyBookings(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var page = new PageRequest(pageNumber, pageSize);
        var result = await bookingService.GetOwnBookingsAsync(page, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{bookingId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BookingDto>>> GetBookingById(
        [FromRoute] Guid bookingId,
        CancellationToken ct = default)
    {
        var result = await bookingService.GetBookingByIdAsync(bookingId, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("code/{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookingDto>>> GetBookingByCode(
        [FromRoute] string code,
        CancellationToken ct = default)
    {
        var result = await bookingService.GetBookingByCodeAsync(code, ct);
        if (result == null)
        {
            return NotFound(ApiResponseFactory.Failure<BookingDto>("Không tìm thấy đơn đặt sân"));
        }

        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{bookingId:guid}/invoice")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(
        [FromRoute] Guid bookingId,
        CancellationToken ct = default)
    {
        var result = await bookingService.GetBookingInvoiceAsync(bookingId, ct);
        return Ok(ApiResponseFactory.Success(result));
    }
}