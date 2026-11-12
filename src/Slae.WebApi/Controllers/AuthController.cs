using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Slae.Application.Interfaces;
using Slae.WebApi.Dtos;
using Slae.WebApi.Services;
using DomainUser = Slae.Domain.Entities.User;

namespace Slae.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.EnhancedHashPassword("dummy-password-for-timing");

    private readonly ISlaeDbContext _db;
    private readonly ITokenService _tokens;

    public AuthController(ISlaeDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        string username = req.Username.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username is already taken");
        }

        var user = new DomainUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(req.Password)
        };
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username is already taken");
        }

        var (token, expires) = _tokens.Create(user);
        return StatusCode(StatusCodes.Status201Created, new AuthResponse(token, expires, user.Username));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == req.Username, ct);

        bool passwordOk = BCrypt.Net.BCrypt.EnhancedVerify(req.Password, user?.PasswordHash ?? DummyHash);

        if (user is null || !passwordOk)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password");
        }

        var (token, expires) = _tokens.Create(user);
        return Ok(new AuthResponse(token, expires, user.Username));
    }
}