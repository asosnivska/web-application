using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slae.Application.Interfaces;
using Slae.Domain.Enums;

namespace Slae.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ISlaeDbContext _db;

    public AdminController(ISlaeDbContext db)
    {
        _db = db;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var running = await _db.Tasks.AsNoTracking()
            .Where(t => t.Status == SlaeTaskStatus.InProgress)
            .OrderBy(t => t.StartedAt)
            .Select(t => new { t.Id, t.User.Username, t.Dimension, t.Progress, t.ServerId, t.StartedAt })
            .ToListAsync(ct);

        var queue = await _db.Tasks.AsNoTracking()
            .Where(t => t.Status == SlaeTaskStatus.Pending && !t.IsCanceled)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.Id, t.User.Username, t.Dimension, t.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { running, queue });
    }
}
