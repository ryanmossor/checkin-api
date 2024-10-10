using CheckinApi.Config;
using CheckinApi.Interfaces;
using CheckinApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace CheckinApi.Controllers;

/// <summary>
/// Endpoints related to check-in queue processing
/// </summary>
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

    /// <summary>
    /// Gets AutoSheets-formatted results strings for a list of dates
    /// </summary>
    /// <param name="dates"></param>
    /// <param name="concatResults"></param>
    /// <param name="delimiter"></param>
    /// <returns>AutoSheets-formatted results strings for a list of dates</returns>
    /// <remarks>
    /// Sample request:
    /// <![CDATA[
    ///     GET /api/checkin?dates=2024-04-01,2024-04-15&concatResults=true
    /// ]]>
    /// </remarks>
    /// <response code="200">AutoSheets-formatted results strings for a list of dates</response>
    [HttpGet]
    public async Task<IActionResult> ProcessCheckinDatesAsync(
        [FromQuery] string dates,
        [FromQuery] bool concatResults,
        [FromQuery] string? delimiter)
    {
        CheckinResponse result = await _processor.ProcessSavedResultsAsync(dates, concatResults, delimiter);
        return new OkObjectResult(result);
    }

    /// <summary>
    /// Processes a list of check-in queue items
    /// </summary>
    /// <returns>Processed results strings formatted for AutoSheets</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/checkin/process
    ///     {
    ///         "checkinFields": {
    ///             "spreadsheetId": "sheet ID",
    ///             "date": "2024-04-30",
    ///             "month": "Apr",
    ///             "cellReference": "AF1"
    ///         },
    ///         "formResponse": {
    ///             "item1": "1",
    ///             "item2": "1",
    ///             "item3": "1"
    ///         },
    ///         "getWeight": true,
    ///         "sleepStart": 0,
    ///         "sleepEnd": 0
    ///     }
    ///
    /// </remarks>
    /// <response code="200">Returns object containing AutoSheets-formatted results strings for each month</response>
    /// <response code="400">If request is incorrectly formatted</response>
    /// <response code="500">If error occured while processing request</response>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessCheckinQueueAsync(
        [FromBody] List<CheckinItem> request,
        [FromQuery] bool concatResults,
        [FromQuery] bool forceProcessing,
        [FromQuery] string? delimiter)
    {
        if (!request.Any())
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

        CheckinResponse result = await _processor.ProcessQueueAsync(
            request.OrderBy(x => x.CheckinFields.Date).ToList(),
            concatResults,
            forceProcessing,
            delimiter);

        return new OkObjectResult(result);
    }

    /// <summary>
    /// Gets full checklist and tracked activities list
    /// </summary>
    /// <returns>Full checklist and tracked activities list</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/checkin/lists
    ///
    /// </remarks>
    /// <response code="200">Returns list of dates with available check-in data</response>
    /// <response code="500">If error occured while processing request</response>
    [HttpGet("lists")]
    public async Task<IActionResult> GetCheckinLists()
    {
        CheckinLists? lists = await _repository.GetCheckinListsAsync();
        return new OkObjectResult(lists);
    }

    /// <summary>
    /// Updates full checklist and tracked activities list
    /// </summary>
    /// <param name="lists"></param>
    /// <returns>Updated check-in lists</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     PATCH /api/checkin/lists
    ///     {
    ///         "fullChecklist": {
    ///             "item1",
    ///             "item2",
    ///             "item3"
    ///         },
    ///         "trackedActivities": {
    ///             "tracked1",
    ///             "tracked2"
    ///         }
    ///     }
    ///
    /// </remarks>
    /// <response code="200">Updated check-in lists</response>
    /// <response code="500">If error occured while processing request</response>
    [HttpPatch("lists")]
    public async Task<IActionResult> UpdateCheckinListsAsync([FromBody] CheckinLists lists)
    {
        _logger.LogDebug("Updating check-in lists: {@lists}", lists);
        CheckinLists result = await _repository.UpdateCheckinListsAsync(lists);
        return new OkObjectResult(result);
    }

    /// <summary>
    /// Gets check-in data for a single date
    /// </summary>
    /// <param name="date"></param>
    /// <returns>CheckinItem object containing data for requested date</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/checkin/date/2024-03-15
    ///
    /// </remarks>
    /// <response code="200">CheckinItem object containing data for requested date</response>
    /// <response code="404">If no dates found matching provided query</response>
    /// <response code="500">If error occured while processing request</response>
    [HttpGet("date/{date}")]
    public async Task<IActionResult> GetCheckinItemByDateAsync([FromRoute] string date)
    {
        try
        {
            CheckinItem? checkinItem = await _repository.GetCheckinItemAsync(date);
            return new OkObjectResult(checkinItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve check-in results for {date}", date);
            return new NotFoundResult();
        }
    }

    /// <summary>
    /// Gets a list of dates for which processed check-in data is available
    /// </summary>
    /// <param name="year"></param>
    /// <param name="month"></param>
    /// <param name="reverse"></param>
    /// <returns>A list of dates with available check-in data for the provided month</returns>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/checkin/2024/03?reverse=true
    ///
    /// </remarks>
    /// <response code="200">Returns list of dates with available check-in data</response>
    /// <response code="404">If no dates found matching provided query</response>
    /// <response code="500">If error occured while processing request</response>
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
