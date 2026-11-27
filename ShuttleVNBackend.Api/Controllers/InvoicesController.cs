using System.ComponentModel.DataAnnotations;
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
[Route("api/invoices")]
[Authorize(Roles = "Employee,Admin")]
public sealed class InvoicesController(InvoiceService invoiceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceDetailDto>>>> Search(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] string? code,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var page = new PageRequest(pageNumber, pageSize);
        var result = await invoiceService.SearchAsync(page, status, code, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{invoiceId:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> Get(
        [FromRoute] Guid invoiceId,
        CancellationToken ct = default)
    {
        var result = await invoiceService.GetAsync(invoiceId, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{invoiceId:guid}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InvoiceDetailDto>>>> History(
        [FromRoute] Guid invoiceId,
        CancellationToken ct = default)
    {
        var result = await invoiceService.HistoryAsync(invoiceId, ct);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>
    ///     Records a full payment already collected outside the system. May settle the booking first if its end time has
    ///     passed.
    /// </summary>
    [HttpPatch("{invoiceId:guid}/pay")]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> Pay(
        [FromRoute] Guid invoiceId,
        [FromBody] PayInvoiceDto dto,
        [FromHeader(Name = "Idempotency-Key")] [Required]
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await invoiceService.PayAsync(invoiceId, dto, idempotencyKey, ct);
        return Ok(ApiResponseFactory.Success(result));
    }
}