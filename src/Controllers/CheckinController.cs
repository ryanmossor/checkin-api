using CheckinApi.Config;
using CheckinApi.Extensions;
using CheckinApi.Interfaces;
using CheckinApi.Models;
using CheckinApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CheckinApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckinController : ControllerBase
{
    private readonly ICheckinLists _lists;
    private readonly ICheckinQueueProcessor _processor;
    private readonly ILogger<CheckinController> _logger;
    private readonly CheckinConfig _config;

    public CheckinController(
        ICheckinQueueProcessor processor,
        ICheckinLists lists,
        ILogger<CheckinController> logger,
        CheckinConfig config)
    {
        _processor = processor;
        _lists = lists;
        _logger = logger;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> ProcessCheckinDatesAsync(
        [FromQuery] string dates,
        [FromQuery] bool concatResults,
        [FromQuery] string? delimiter)
    {
        var result = await _processor.ProcessSavedResultsAsync(dates, concatResults, delimiter);
        return new OkObjectResult(result);
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCheckinQueueAsync(
        [FromBody] CheckinRequest request,
        [FromQuery] bool concatResults,
        [FromQuery] bool forceProcessing,
        [FromQuery] string? delimiter)
    {
        if (!request.Queue.Any())
        {
            return new BadRequestObjectResult("No items in check-in queue");
        }

        try
        {
            var json = request.Serialize();
            var filename = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            await System.IO.File.WriteAllTextAsync(Path.Combine(_config.RequestsDir, filename), json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing request to file");
        }

        var result = await _processor.ProcessQueueAsync(
            request.Queue.OrderBy(x => x.CheckinFields.Date).ToList(),
            concatResults,
            forceProcessing,
            delimiter);

        return new OkObjectResult(result);
    }

    [HttpPost("single")] // test, remove
    public async Task<IActionResult> ProcessSingleCheckinItemAsync(
        [FromBody] CheckinItem item,
        [FromQuery] bool concatResults,
        [FromQuery] bool forceProcessing,
        [FromQuery] string? delimiter)
    {
        var result = await _processor.ProcessQueueAsync(new List<CheckinItem> { item }, concatResults, forceProcessing, delimiter);
        return new OkObjectResult(result);
    }

    [HttpGet("lists")]
    public IActionResult GetCheckinLists() => new OkObjectResult(_lists);

    [HttpPatch("lists")]
    public async Task<IActionResult> UpdateCheckinListsAsync([FromBody] CheckinLists lists)
    {
        _logger.LogDebug("Updating check-in lists: {@lists}", lists);
        var result = await _lists.UpdateListsAsync(lists);
        return new OkObjectResult(result);
    }

    [HttpGet("date/{date}")]
    public async Task<IActionResult> GetCheckinItemByDateAsync([FromRoute] string date)
    {
        try
        {
            var item = await System.IO.File.ReadAllTextAsync(Path.Combine(_config.ResultsDir, $"{date}.json"));
            return new OkObjectResult(item.Deserialize<CheckinItem>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve check-in results for {date}", date);
            return new NotFoundResult();
        }
    }

    [HttpGet("{year}/{month}")]
    public IActionResult GetItemsByMonth([FromRoute] string year, [FromRoute] string month, [FromQuery] bool reverse)
    {
        try
        {
            var files = Directory.GetFiles(_config.ResultsDir)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(filename => filename!.StartsWith($"{year}-{month}"))
                .ToList();

            if (!files.Any())
            {
                return new NotFoundObjectResult("Unable to retrieve check-in results for provided query");
            }

            if (reverse)
            {
                return new OkObjectResult(new { Files = files.Select(Path.GetFileNameWithoutExtension).OrderDescending() });
            }

            return new OkObjectResult(new { Files = files.Select(Path.GetFileNameWithoutExtension).Order() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve check-in results for {year}/{month}", year, month);
            return StatusCode(500);
        }
    }
}
