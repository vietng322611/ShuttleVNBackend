using Microsoft.AspNetCore.Authentication.Cookies;
using ShuttleVNBackend.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "shuttlevn.auth";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"))
    .AddPolicy("StaffOnly", policy => policy.RequireRole("Employee", "Admin"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();