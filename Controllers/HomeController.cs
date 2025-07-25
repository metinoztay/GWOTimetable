using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class HomeController : Controller
    {
        private readonly Db12026Context _context;

        public HomeController()
        {
            _context = new Db12026Context();
        }


        public IActionResult Dashboard()
        {
            Workspace ws = new Workspace();
            ws.WorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            var userId = User.FindFirst("UserId")?.Value;
            bool isAnyWorkspace = _context.Workspaces.Any(w => w.WorkspaceId == ws.WorkspaceId && w.UserId.ToString() == userId);
            if (!isAnyWorkspace)
                return RedirectToAction("Logout", "Account");
            else
            {
                ViewBag.ActiveTabId = "Dashboard";
                return View();
            }
        }
    }
}

