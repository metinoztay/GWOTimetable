using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GWOTimetable.Controllers
{
    public class PrivacyController : Controller
    {
        //private readonly GwotimetableDbContext _context;
        /*
                public PrivacyController()
                {
                    _context = new GwotimetableDbContext();
                }*/

        public async Task<IActionResult> Privacy()
        {

            return View();
        }
    }
}
