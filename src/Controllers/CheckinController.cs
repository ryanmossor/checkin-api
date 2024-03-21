using CheckinApi.Extensions;
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
    
    public CheckinController(ICheckinQueueProcessor processor, ICheckinLists lists, ILogger<CheckinController> logger)
    {
        _processor = processor;
        _lists = lists;
        _logger = logger;
    }

    [HttpGet]
    public string SayHello()
    {
        return "Hello world\n";
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCheckinQueue([FromBody] CheckinRequest? request, [FromQuery] string? dates)
    {
        if (dates != null)
        {
            _logger.LogDebug("Dates to process: {dates}", dates);
            var queue = new List<CheckinItem>();
            foreach (var date in dates.Split(',').Order())
            {
                try
                {
                    var contents = await System.IO.File.ReadAllTextAsync($"./data/results/{date}.json");
                    _logger.LogDebug("Adding results for {date} to queue", date);
                    queue.Add(contents.Deserialize<CheckinItem>());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving data for {date}", date);
                }
            }

            var result = await _processor.Process(queue);
            return new OkObjectResult(result);
        }

        if (request != null && request.Queue.Any())
        {
            var result = await _processor.Process(request.Queue.OrderBy(x => x.CheckinFields.Date).ToList());
            return new OkObjectResult(result);
        }

        return new BadRequestResult();
    }

    [HttpPost("single")] // test, remove
    public async Task<IActionResult> ProcessSingleCheckinItem([FromBody] CheckinItem item)
    {
        var result = await _processor.Process(new List<CheckinItem> { item });
        return new OkObjectResult(result);
    }
    
    [HttpGet("lists")]
    public IActionResult GetCheckinLists()
    {
        return new OkObjectResult(_lists.GetLists());
    }

    [HttpPost("lists")]
    public async Task<IActionResult> UpdateCheckinLists([FromBody] CheckinLists lists)
    {
        var result = await _lists.UpdateLists(lists.FullChecklist, lists.TrackedActivities);
        return new OkObjectResult(result);
    }

    [HttpGet("date/{date}")]
    public async Task<IActionResult> GetCheckinItemByDate([FromRoute] string date)
    {
        try
        {
            var item = await System.IO.File.ReadAllTextAsync($"./data/results/{date}.json");
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
            var files = Directory.GetFiles("./data/results")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(filename => filename!.StartsWith($"{year}-{month}"));

            if (reverse)
            {
                return new OkObjectResult(new { Files = files.Select(Path.GetFileNameWithoutExtension).OrderDescending() });
            }

            return new OkObjectResult(new { Files = files.Select(Path.GetFileNameWithoutExtension).Order()});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve check-in results for {year}/{month}", year, month);
            return new NoContentResult();
        }
    }
}