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
    
    [HttpGet("process")]
    public async Task<IActionResult> ProcessCheckinDatesAsync([FromQuery] string dates) 
    {
        var result = await _processor.ProcessSavedResultsAsync(dates);
        return new OkObjectResult(result);
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCheckinQueueAsync([FromBody] CheckinRequest request)
    {
        if (!request.Queue.Any())
            return new BadRequestObjectResult("No items in check-in queue");
        
        try
        {
            var json = request.Serialize();
            var filename = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            await System.IO.File.WriteAllTextAsync(Path.Combine(Constants.RequestsDir, filename), json);
        } 
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing request to file");
        }
            
        var result = await _processor.ProcessQueueAsync(request.Queue.OrderBy(x => x.CheckinFields.Date).ToList());
        return new OkObjectResult(result);
    }

    [HttpPost("single")] // test, remove
    public async Task<IActionResult> ProcessSingleCheckinItemAsync([FromBody] CheckinItem item)
    {
        var result = await _processor.ProcessQueueAsync(new List<CheckinItem> { item });
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
