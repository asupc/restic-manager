using Microsoft.AspNetCore.Mvc;

namespace ResticManager.Web.Controllers
{
    public class BackupFilesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
