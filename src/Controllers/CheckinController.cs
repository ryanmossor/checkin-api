using CheckinApi.Config;
using CheckinApi.Interfaces;
using CheckinApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace CheckinApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckinController : ControllerBase
{
    private readonly ICheckinQueueProcessor _processor;
    private readonly ILogger<CheckinController> _logger;
    private readonly CheckinConfig _config;
    private readonly ICheckinRepository _repository;

    public CheckinController(
        ICheckinQueueProcessor processor,
        ILogger<CheckinController> logger,
        CheckinConfig config,
        ICheckinRepository repository)
    {
        _processor = processor;
        _logger = logger;
        _config = config;
        _repository = repository;
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
            await _repository.SaveCheckinRequestAsync(request);
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
    public async Task<IActionResult> GetCheckinLists()
    {
        var lists = await _repository.GetCheckinListsAsync();
        return new OkObjectResult(lists);
    }

    [HttpPatch("lists")]
    public async Task<IActionResult> UpdateCheckinListsAsync([FromBody] CheckinLists lists)
    {
        _logger.LogDebug("Updating check-in lists: {@lists}", lists);
        var result = await _repository.UpdateCheckinListsAsync(lists);
        return new OkObjectResult(result);
    }

    [HttpGet("date/{date}")]
    public async Task<IActionResult> GetCheckinItemByDateAsync([FromRoute] string date)
    {
        try
        {
            var checkinItem = await _repository.GetCheckinItemAsync(date);
            return new OkObjectResult(checkinItem);
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
