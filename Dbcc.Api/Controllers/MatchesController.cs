using Dbcc.Api.Contracts;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dbcc.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(IMatchService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchModel>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{**id}")]
    public async Task<ActionResult<MatchModel>> Get(string id, CancellationToken ct)
    {
        var match = await service.GetAsync(id, ct);
        return match is null ? NotFound(new ErrorResultModel("Match not found.")) : Ok(match);
    }

    // Match ids are composed from the frontend's dd/mm/yy date and therefore
    // contain slashes. The query-based aliases are reliable for encoded ids.
    [HttpGet("item")]
    public Task<ActionResult<MatchModel>> GetItem([FromQuery] string id, CancellationToken ct) => Get(id, ct);

    [HttpPut("item")]
    public Task<IActionResult> UpdateItem([FromQuery] string id, [FromBody] MatchModel match, CancellationToken ct) => Update(id, match, ct);

    [HttpDelete("item")]
    public Task<IActionResult> DeleteItem([FromQuery] string id, CancellationToken ct) => Delete(id, ct);

    [HttpPut]
    public async Task<IActionResult> ReplaceAll([FromBody] List<MatchModel> matches, CancellationToken ct)
    {
        if (!await service.ReplaceAllAsync(matches, ct))
            return BadRequest(new ErrorResultModel("Expected matches with unique ids, a date, a match number, valid points, and valid expenses referencing existing players."));
        return Ok(new OkResultModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MatchModel match, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(match.Id) || string.IsNullOrWhiteSpace(match.Date) || string.IsNullOrWhiteSpace(match.MatchNumber))
            return BadRequest(new ErrorResultModel("Match id, date, and match number are required."));
        if (!await service.CreateAsync(match, ct)) return Conflict(new ErrorResultModel("The match is invalid, references a missing player, or already exists."));
        return StatusCode(StatusCodes.Status201Created, match);
    }

    [HttpPut("{**id}")]
    public async Task<IActionResult> Update(string id, [FromBody] MatchModel match, CancellationToken ct)
    {
        if (!await service.UpdateAsync(id, match, ct))
            return string.Equals(id, match.Id, StringComparison.Ordinal)
                ? NotFound(new ErrorResultModel("Match not found or match data is invalid."))
                : BadRequest(new ErrorResultModel("Route id must match the match id."));
        return Ok(new OkResultModel());
    }

    [HttpDelete("{**id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound(new ErrorResultModel("Match not found."));
}
