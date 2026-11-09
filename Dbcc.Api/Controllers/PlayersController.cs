using Dbcc.Api.Contracts;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dbcc.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController(IPlayerService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlayerModel>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlayerModel>> Get(int id, CancellationToken ct)
    {
        var player = await service.GetAsync(id, ct);
        return player is null ? NotFound(new ErrorResultModel("Player not found.")) : Ok(player);
    }

    [HttpPut]
    public async Task<IActionResult> ReplaceAll([FromBody] List<PlayerModel> players, CancellationToken ct)
    {
        if (players.Count != 16 || !PlayerService.ValidPlayers(players))
            return BadRequest(new ErrorResultModel("Expected exactly 16 players with unique positive ids, names, and finite batting, bowling, and fielding scores."));
        await service.ReplaceAllAsync(players, ct);
        return Ok(new OkResultModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlayerModel player, CancellationToken ct)
    {
        if (player.Id <= 0 || string.IsNullOrWhiteSpace(player.Name) || !double.IsFinite(player.Batting) || !double.IsFinite(player.Bowling) || !double.IsFinite(player.Fielding))
            return BadRequest(new ErrorResultModel("Player id, name, and finite category scores are required."));
        if (!await service.CreateAsync(player, ct)) return Conflict(new ErrorResultModel("A player with that id already exists."));
        return CreatedAtAction(nameof(Get), new { id = player.Id }, player);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PlayerModel player, CancellationToken ct)
    {
        if (!await service.UpdateAsync(id, player, ct))
            return player.Id != id ? BadRequest(new ErrorResultModel("Route id must match the player id.")) : NotFound(new ErrorResultModel("Player not found or player data is invalid."));
        return Ok(new OkResultModel());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound(new ErrorResultModel("Player not found."));
}
