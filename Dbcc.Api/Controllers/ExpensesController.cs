using Dbcc.Api.Contracts;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dbcc.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(IExpenseService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ExpenseStateModel>> Get(CancellationToken ct) => Ok(await service.GetStateAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Replace([FromBody] ExpenseStateModel state, CancellationToken ct)
    {
        if (!ExpenseService.ValidState(state))
            return BadRequest(new ErrorResultModel("Expected an object with a periods array containing unique period ids, income arrays, and expenses arrays."));
        await service.ReplaceStateAsync(state, ct);
        return Ok(new OkResultModel());
    }

    [HttpGet("periods")]
    public async Task<IActionResult> GetPeriods(CancellationToken ct) => Ok((await service.GetStateAsync(ct)).Periods);

    [HttpPost("periods")]
    public async Task<IActionResult> CreatePeriod([FromBody] ExpensePeriodModel period, CancellationToken ct)
    {
        if (!await service.CreatePeriodAsync(period, ct)) return Conflict(new ErrorResultModel("The period is invalid or already exists."));
        var saved = await service.GetPeriodAsync(period.Id, ct);
        return CreatedAtAction(nameof(GetPeriod), new { id = period.Id }, saved);
    }

    [HttpGet("periods/{id}")]
    public async Task<IActionResult> GetPeriod(string id, CancellationToken ct)
    {
        var period = await service.GetPeriodAsync(id, ct);
        return period is null ? NotFound(new ErrorResultModel("Expense period not found.")) : Ok(period);
    }

    [HttpPut("periods/{id}")]
    public async Task<IActionResult> UpdatePeriod(string id, [FromBody] ExpensePeriodModel period, CancellationToken ct)
    {
        if (id != period.Id) return BadRequest(new ErrorResultModel("Route id must match the period id."));
        return await service.UpdatePeriodAsync(id, period, ct)
            ? Ok(new OkResultModel())
            : NotFound(new ErrorResultModel("Expense period not found or invalid."));
    }

    [HttpDelete("periods/{id}")]
    public async Task<IActionResult> DeletePeriod(string id, CancellationToken ct) =>
        await service.DeletePeriodAsync(id, ct) ? NoContent() : NotFound(new ErrorResultModel("Expense period not found."));

    [HttpGet("periods/{periodId}/items/{kind}")]
    public async Task<IActionResult> GetItems(string periodId, string kind, CancellationToken ct)
    {
        var period = await service.GetPeriodAsync(periodId, ct);
        if (period is null) return NotFound(new ErrorResultModel("Expense period not found."));
        var items = SelectItems(period, kind);
        return items is null ? BadRequest(new ErrorResultModel("Item kind must be income or expense.")) : Ok(items);
    }

    [HttpGet("periods/{periodId}/items/{kind}/{itemId}")]
    public async Task<IActionResult> GetItem(string periodId, string kind, string itemId, CancellationToken ct)
    {
        var itemsResponse = await GetItems(periodId, kind, ct);
        if (itemsResponse is not OkObjectResult { Value: IEnumerable<ExpenseLineItemModel> items }) return itemsResponse;
        var item = items.FirstOrDefault(x => x.Id == itemId);
        return item is null ? NotFound(new ErrorResultModel("Expense line item not found.")) : Ok(item);
    }

    [HttpPost("periods/{periodId}/items/{kind}")]
    public async Task<IActionResult> AddItem(string periodId, string kind, [FromBody] ExpenseLineItemModel item, CancellationToken ct)
    {
        if (!await service.AddLineItemAsync(periodId, kind, item, ct))
            return BadRequest(new ErrorResultModel("Expense period or item kind is invalid, or the item id already exists."));
        var period = await service.GetPeriodAsync(periodId, ct);
        var saved = period is null ? null : SelectItems(period, kind)?.FirstOrDefault(x => x.Id == item.Id);
        if (saved is null) return StatusCode(StatusCodes.Status201Created);
        return CreatedAtAction(nameof(GetItem), new { periodId, kind, itemId = saved.Id }, saved);
    }

    [HttpPut("periods/{periodId}/items/{kind}/{itemId}")]
    public async Task<IActionResult> UpdateItem(string periodId, string kind, string itemId, [FromBody] ExpenseLineItemModel item, CancellationToken ct) =>
        await service.UpdateLineItemAsync(periodId, itemId, kind, item, ct)
            ? Ok(new OkResultModel())
            : NotFound(new ErrorResultModel("Expense line item not found or invalid."));

    [HttpDelete("periods/{periodId}/items/{kind}/{itemId}")]
    public async Task<IActionResult> DeleteItem(string periodId, string kind, string itemId, CancellationToken ct) =>
        await service.DeleteLineItemAsync(periodId, itemId, kind, ct)
            ? NoContent()
            : NotFound(new ErrorResultModel("Expense line item not found."));

    private static List<ExpenseLineItemModel>? SelectItems(ExpensePeriodModel period, string kind) => kind.ToLowerInvariant() switch
    {
        "income" => period.Income,
        "expense" or "expenses" => period.Expenses,
        _ => null,
    };
}
