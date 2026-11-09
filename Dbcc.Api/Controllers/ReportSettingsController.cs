using System.Text.Json;
using Dbcc.Api.Contracts;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dbcc.Api.Controllers;

[ApiController]
[Route("api/report-settings")]
public sealed class ReportSettingsController(IReportSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await service.GetAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JsonElement settings, CancellationToken ct) =>
        await service.SaveAsync(settings, ct)
            ? Ok(new OkResultModel())
            : BadRequest(new ErrorResultModel("Expected a settings object with text monthLabel/tillDate/customMessage, number or text counts, and a boolean showWinsLosses."));

    [HttpDelete]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await service.ResetAsync(ct);
        return NoContent();
    }
}
