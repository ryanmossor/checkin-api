using Microsoft.AspNetCore.Mvc;

namespace CheckinApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetCheck()
    {
        return new OkResult();
    }
}