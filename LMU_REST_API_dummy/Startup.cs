using Microsoft.AspNetCore.Mvc;

namespace CrewChiefV4.LMU
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    /// <summary>
    /// Generalise responding to a REST Get
    /// </summary>
    internal class response : ControllerBase
    {
        internal IActionResult getResponse<T>(string filePath)
        {
            var data = JsonFiles.ReadFile<LMU_REST_API_classes.RepairAndRefuel.Root>(filePath);
            if (data != null)
            {
                return Ok(data);
            }
            else
            {
                return NotFound($"'{Path.GetFullPath(filePath)}' not found.");
            }
        }
    }
    [ApiController]
    [Route("rest/garage/UIScreen/[controller]")]
    public class RepairAndRefuelController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetRepairAndRefuel()
        {
            return new response().getResponse<LMU_REST_API_classes.RepairAndRefuel.Root>(
                "LMU_REST_API_classes.RepairAndRefuel.json");
        }
    }

    [ApiController]
    [Route("rest/options/setting/[controller]")]
    public class DRIVEAIDS_invulnerableController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetDriveAidsInvulnerable()
        {
            return new response().getResponse<LMU_REST_API_classes.OptionsSettings.Root>(
                "LMU_REST_API_classes.Options.Sessions.json");
        }
    }
    [ApiController]

    [Route("rest/[controller]")]
    public class Sessions : ControllerBase
    {
        [HttpGet]
        public IActionResult GetSessions()
        {
            return new response().getResponse<LMU_REST_API_classes.Sessions.Root>(
                "LMU_REST_API_classes.Sessions.json");
        }
    }

}
