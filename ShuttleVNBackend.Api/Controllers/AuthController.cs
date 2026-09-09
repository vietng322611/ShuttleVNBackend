using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.UseCases.User.Services;
using ShuttleVNBackend.Core.Entities.User.Enums;
using AppAuthService = ShuttleVNBackend.Application.UseCases.Authentication.Services.AuthenticationService;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    AppAuthService authenticationService,
    AccountService accountService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        try
        {
            var account = await accountService.Register(dto);
            return Ok(new { accountId = account.AccountId });
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

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        try
        {
            var account = await authenticationService.VerifyLogin(dto);

            var role = account.AccountType == AccountType.Customer ? "Customer" : "Employee";
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
                new(ClaimTypes.Email, account.LoginEmail),
                new(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return Ok(new { message = "Logged in" });
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(new ProblemDetails { Title = ex.Message });
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out" });
    }

    [HttpPost("issue-code")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueCode([FromBody] CodeRequestDto dto)
    {
        try
        {
            var code = await authenticationService.IssueCode(dto.Email, dto.Type);
            // return for testing. Implement EmailService later
            return Ok(new { code });
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await authenticationService.ResetPassword(dto);
            return Ok(new { message = "Password reset successfully" });
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(new ProblemDetails { Title = ex.Message });
        }
    }
}
