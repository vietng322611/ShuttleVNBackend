using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Api.DTOs.Users;
using ShuttleVNBackend.Api.Extensions;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.UseCases.Authentication.Services;
using ShuttleVNBackend.Application.UseCases.Users.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppAuthService appAuthService,
    AccountService accountService,
    PasswordSetupService passwordSetupService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<dynamic>>> Register(
        [FromBody] RegisterDto dto)
    {
        var account = await accountService.Register(dto);
        return Created("", ApiResponseFactory.Success(new
        {
            accountId = account.AccountId
        }));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Login(
        [FromBody] LoginDto dto,
        CancellationToken ct)
    {
        var account = await appAuthService.VerifyLogin(dto, ct);

        var role = account.GetRole();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new(ClaimTypes.Email, account.LoginEmail),
            new(ClaimTypes.Role, role),
            new("IsAdmin", (account.Employee?.IsAdmin ?? false).ToString().ToLowerInvariant())
        };

        if (account.Employee is not null)
            claims.Add(new Claim("EmployeeId", account.Employee.EmployeeId.ToString()));
        // Yes I did include the Customer table
        else if (account.Customer is not null)
            claims.Add(new Claim("CustomerId", account.Customer.CustomerId.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Ok(ApiResponseFactory.Success(AccountDto.FromEntity(account)));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPost("issue-code")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> IssueCode(
        [FromBody] CodeRequestDto dto,
        CancellationToken ct)
    {
        await appAuthService.IssueCodeAsync(dto.Email, dto.Type, ct);
        return Ok(ApiResponseFactory.Success<object>(new
        {
            message = "Verification code sent to your email."
        }));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<dynamic>>> ResetPassword(
        [FromBody] ResetPasswordDto dto)
    {
        await appAuthService.ResetPassword(dto);
        return Ok(ApiResponseFactory.Success(new
        {
            message = "Password reset successfully"
        }));
    }

    [HttpPost("set-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<dynamic>>> SetPassword(
        [FromBody] SetPasswordDto dto,
        CancellationToken ct)
    {
        await passwordSetupService.SetPasswordAsync(dto, ct);
        return Ok(ApiResponseFactory.Success(new
        {
            message = "Password set successfully. You can now sign in."
        }));
    }

    [HttpPost("resend-setup-link")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<dynamic>>> ResendSetupLink(
        [FromBody] ResendSetupLinkDto dto,
        CancellationToken ct)
    {
        await passwordSetupService.ResendAsync(dto, ct);
        return Ok(ApiResponseFactory.Success(new
        {
            message = "If the email belongs to an account awaiting activation, a new link has been sent."
        }));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AccountDto>>> GetMe()
    {
        var account = await accountService.GetAccountById(User.GetAccountId());
        return Ok(ApiResponseFactory.Success(AccountDto.FromEntity(account)));
    }
}
