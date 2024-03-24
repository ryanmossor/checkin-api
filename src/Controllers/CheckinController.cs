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
    public async Task<IActionResult> ProcessCheckinQueueAsync([FromBody] CheckinRequest? request, [FromQuery] string? dates)
    {
        if (dates != null)
        {
            _logger.LogDebug("Dates to process: {dates}", dates);
            var queue = new List<CheckinItem>();
            foreach (var date in dates.Split(',').Order())
            {
                try
                {
                    var contents = await System.IO.File.ReadAllTextAsync(Path.Combine(Constants.ResultsDir, $"{date}.json"));
                    queue.Add(contents.Deserialize<CheckinItem>());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving data for {date}", date);
                }
            }

            var result = await _processor.ProcessAsync(queue);
            return new OkObjectResult(result);
        }

        if (request != null && request.Queue.Any())
        {
            try
            {
                var json = request.SerializeFlat().Replace("\\u003C", "<");
                var filename = $"{DateTime.Now:yyyy-MM-dd_hh:mm:ss}.json";
                await System.IO.File.WriteAllTextAsync(Path.Combine(Constants.RequestsDir, filename), json);
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing request to file");
            }
            
            var result = await _processor.ProcessAsync(request.Queue.OrderBy(x => x.CheckinFields.Date).ToList());
            return new OkObjectResult(result);
        }

        return new BadRequestResult();
    }

    [HttpPost("single")] // test, remove
    public async Task<IActionResult> ProcessSingleCheckinItemAsync([FromBody] CheckinItem item)
    {
        var result = await _processor.ProcessAsync(new List<CheckinItem> { item });
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
            var item = await System.IO.File.ReadAllTextAsync(Path.Combine(Constants.ResultsDir, $"{date}.json"));
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
            var files = Directory.GetFiles(Constants.ResultsDir)
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
