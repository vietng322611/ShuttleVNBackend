using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.Extensions;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Api.DTOs.Users;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.UseCases.Users.Services;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountController(
    AccountService accountService,
    CustomerService customerService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountDto>>>> ListAccounts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var page = new PageRequest(pageNumber, pageSize);
        var pagedResult = await accountService.GetAllAccounts(page);
        return Ok(ApiResponseFactory.Success(pagedResult));
    }

    [HttpPost("{accountId:guid}/lock")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<dynamic>>> LockAccount(
        [FromRoute] Guid accountId)
    {
        await accountService.UpdateAccountStatus(accountId, AccountStatus.Disabled, User.IsAdmin());
        return Ok(ApiResponseFactory.Success(new
        {
            message = "Account locked"
        }));
    }

    [HttpPost("{accountId:guid}/unlock")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<dynamic>>> UnlockAccount(
        [FromRoute] Guid accountId)
    {
        await accountService.UpdateAccountStatus(accountId, AccountStatus.Active, User.IsAdmin());
        return Ok(ApiResponseFactory.Success(new
        {
            message = "Account unlocked"
        }));
    }

    [HttpGet("customers")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerListItemDto>>>> ListCustomers(
        [FromQuery] string? keyword,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var page = new PageRequest(pageNumber, pageSize);
        return Ok(ApiResponseFactory.Success(await customerService.SearchAsync(keyword, page, ct)));
    }

    [HttpGet("customers/{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<CustomerListItemDto>>> GetCustomer(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await customerService.GetByIdAsync(id, ct)));
    }

    [HttpGet("customers/{id:guid}/bookings")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingListItemDto>>>> GetCustomerBookings(
        [FromRoute] Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var page = new PageRequest(pageNumber, pageSize);
        return Ok(ApiResponseFactory.Success(await customerService.GetBookingsAsync(id, page, ct)));
    }

    [HttpPost("customers")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<CustomerListItemDto>>> CreateCustomerAccount(
        [FromBody] CustomerProfileDto dto,
        CancellationToken ct = default)
    {
        var created = await accountService.CreateCustomerAccountAsync(dto, ct);
        return CreatedAtAction(
            nameof(GetCustomer),
            new { id = created.CustomerId },
            ApiResponseFactory.Success(created));
    }

    [HttpPut("customers/{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<CustomerListItemDto>>> UpdateCustomer(
        [FromRoute] Guid id,
        [FromBody] CustomerProfileDto dto,
        CancellationToken ct = default)
    {
        return Ok(ApiResponseFactory.Success(await customerService.UpdateCustomer(id, dto, ct)));
    }
}
