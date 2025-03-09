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
    public class TimetableController : Controller
    {
        private readonly Db12026Context _context;

        public TimetableController()
        {
            _context = new Db12026Context();
        }


        public async Task<IActionResult> Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var ws = _context.Workspaces.Where(w => w.WorkspaceId == selectedWorkspaceId);
            var timetables = _context.Timetables.Include(t => t.TimetableState).Where(t => t.WorkspaceId == selectedWorkspaceId).ToList();

            ViewBag.ActiveTabId = "TimetableManagement";
            return View(timetables);
        }
    }
}

