using Dbcc.Api.Contracts;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dbcc.Api.Controllers;

[ApiController]
[Route("api/collections")]
public sealed class CollectionsController(ICollectionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CollectionModel>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CollectionModel>> Get(int id, CancellationToken ct)
    {
        var collection = await service.GetAsync(id, ct);
        return collection is null ? NotFound(new ErrorResultModel("Collection not found.")) : Ok(collection);
    }

    [HttpPut]
    public async Task<IActionResult> ReplaceAll([FromBody] List<CollectionModel> collections, CancellationToken ct)
    {
        if (collections.Count != 16 || collections.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name) || !double.IsFinite(x.AmountPaid)) ||
            collections.Select(x => x.Id).Distinct().Count() != collections.Count)
            return BadRequest(new ErrorResultModel("Expected exactly 16 collection entries with unique positive player ids, names, and finite amounts."));
        if (!await service.ReplaceAllAsync(collections, ct)) return BadRequest(new ErrorResultModel("Every collection must reference an existing player."));
        return Ok(new OkResultModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CollectionModel collection, CancellationToken ct)
    {
        if (!double.IsFinite(collection.AmountPaid)) return BadRequest(new ErrorResultModel("Amount paid must be a finite number."));
        if (!await service.CreateAsync(collection, ct)) return Conflict(new ErrorResultModel("The player does not exist or already has a collection entry."));
        var result = await service.GetAsync(collection.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = collection.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CollectionModel collection, CancellationToken ct)
    {
        if (id != collection.Id) return BadRequest(new ErrorResultModel("Route id must match the collection player id."));
        if (!double.IsFinite(collection.AmountPaid)) return BadRequest(new ErrorResultModel("Amount paid must be a finite number."));
        return await service.UpdateAsync(id, collection, ct)
            ? Ok(new OkResultModel())
            : NotFound(new ErrorResultModel("Collection not found."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await service.DeleteAsync(id, ct) ? NoContent() : NotFound(new ErrorResultModel("Collection not found."));
}
