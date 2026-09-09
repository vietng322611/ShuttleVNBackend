using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.User;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.UseCases.User.Services;
using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("accounts")]
public class AccountsController(
    AccountService accountService,
    CustomerService customerService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> ListAccounts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var page = new PageRequest(pageNumber, pageSize);
        var accounts = await accountService.GetAllAccounts(page);
        return Ok(new
        {
            Accounts = accounts,
            PageNumber = pageNumber,
            TotalCount = accounts.Count
        });
    }

    [HttpPost("{accountId:guid}/lock")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> LockAccount([FromRoute] Guid accountId)
    {
        try
        {
            await accountService.UpdateAccountStatus(accountId, AccountStatus.Disabled);
            return Ok(new { message = "Account locked" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpPost("{accountId:guid}/unlock")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UnlockAccount([FromRoute] Guid accountId)
    {
        try
        {
            await accountService.UpdateAccountStatus(accountId, AccountStatus.Active);
            return Ok(new { message = "Account unlocked" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpGet("customers")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> ListCustomers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var page = new PageRequest(pageNumber, pageSize);
        var customers = await customerService.GetAllCustomers(page);
        return Ok(new
        {
            Customers = customers,
            PageNumber = pageNumber,
            TotalCount = customers.Count
        });
    }

    [HttpGet("customers/{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetCustomer([FromRoute] Guid id)
    {
        var customer = await customerService.GetCustomerById(id);
        if (customer is null) return NotFound(new ProblemDetails { Title = "Resource not found" });
        return Ok(customer);
    }

    // Create customer (profile-only)
    [HttpPost("customers")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerProfileDto dto)
    {
        try
        {
            var created = await customerService.CreateCustomer(dto);
            return CreatedAtAction(nameof(GetCustomer), new { id = created.CustomerId }, created);
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
        catch (ConflictException ex)
        {
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpPut("customers/{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateCustomer([FromRoute] Guid id, [FromBody] CustomerProfileDto dto)
    {
        try
        {
            var updated = await customerService.UpdateCustomer(id, dto);
            return Ok(updated);
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message });
        }
    }
}
