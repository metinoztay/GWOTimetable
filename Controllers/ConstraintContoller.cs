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
    public class ConstraintController : Controller
    {
        private readonly Db12026Context _context;

        public ConstraintController()
        {
            _context = new Db12026Context();
        }



        [HttpPost]
        public async Task<IActionResult> GetConstraintsForEducator([FromBody] ClassCourse classCourse)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            Workspace workspace = _context.Workspaces
            .Include(w => w.Days)
            .Include(w => w.Lessons)
            .Include(w => w.ClassCourses.Where(c => c.ClassCourseId == classCourse.ClassCourseId)).ThenInclude(c => c.TimetableConstraints)
            .Include(w => w.ClassCourses.Where(c => c.ClassCourseId == classCourse.ClassCourseId)).ThenInclude(c => c.Educator.EducatorConstraints)
            .Include(w => w.ClassCourses.Where(c => c.ClassCourseId == classCourse.ClassCourseId)).ThenInclude(c => c.Class.ClassConstraints)
            .Include(w => w.ClassCourses.Where(c => c.ClassCourseId == classCourse.ClassCourseId)).ThenInclude(c => c.Classroom.ClassroomConstraints)
            .FirstOrDefault(w => w.WorkspaceId == selectedWorkspaceId);

            return PartialView("_ConstraintsEducator", workspace);
        }
    }
}

