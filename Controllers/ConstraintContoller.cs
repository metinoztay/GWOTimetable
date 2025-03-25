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

            Workspace workspace = new Workspace();
            if (classCourse.ClassCourseId == 0)
            {
                workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == classCourse.EducatorId))
                .Include(w => w.ClassCourses.Where(c => c.EducatorId == classCourse.EducatorId))
                    .ThenInclude(c => c.TimetableConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
            }
            else
            {
                classCourse = _context.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == classCourse.ClassCourseId);

                workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == classCourse.EducatorId))
                .Include(w => w.ClassCourses.Where(c => c.EducatorId == classCourse.EducatorId))
                    .ThenInclude(c => c.TimetableConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .Include(w => w.ClassroomConstraints.Where(cr => cr.ClassroomId == classCourse.ClassroomId))
                .Include(w => w.ClassConstraints.Where(c => c.ClassId == classCourse.ClassId))
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
            }

            return PartialView("_ConstraintsEducator", workspace);
        }

        [HttpPost]
        public async Task<IActionResult> AddConstraintForEducator([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            if (constraint.ClassCourseId == 0)
            {
                EducatorConstraint educatorConstraint = new EducatorConstraint();
                educatorConstraint.WorkspaceId = selectedWorkspaceId;
                educatorConstraint.EducatorId = constraint.EducatorId;
                educatorConstraint.DayId = constraint.DayId;
                educatorConstraint.LessonId = constraint.LessonId;
                educatorConstraint.IsPlaceable = false;
                _context.EducatorConstraints.Add(educatorConstraint);
                await _context.SaveChangesAsync();
            }
            else
            {
                TimetableConstraint tc = new TimetableConstraint();
                tc.WorkspaceId = selectedWorkspaceId;
                tc.DayId = constraint.DayId;
                tc.LessonId = constraint.LessonId;
                tc.ClassCourseId = constraint.ClassCourseId;
                _context.TimetableConstraints.Add(tc);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveConstraintForEducator([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            EducatorConstraint educatorConstraint = _context.EducatorConstraints.FirstOrDefault(ec => ec.EducatorId == constraint.EducatorId && ec.DayId == constraint.DayId && ec.LessonId == constraint.LessonId);
            if (educatorConstraint != null)
            {
                _context.EducatorConstraints.Remove(educatorConstraint);
                await _context.SaveChangesAsync();
            }

            TimetableConstraint timetableConstraint = _context.TimetableConstraints.FirstOrDefault(tc => tc.DayId == constraint.DayId && tc.LessonId == constraint.LessonId);
            if (timetableConstraint != null)
            {
                _context.TimetableConstraints.Remove(timetableConstraint);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> ClearConstraintsForEducator([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            var educatorConstraints = _context.EducatorConstraints.Where(ec => ec.EducatorId == constraint.EducatorId && ec.WorkspaceId == selectedWorkspaceId).ToList();
            foreach (var ec in educatorConstraints)
            {
                _context.EducatorConstraints.Remove(ec);
            }
            await _context.SaveChangesAsync();

            var ClassCourses = _context.ClassCourses.Where(cc => cc.EducatorId == constraint.EducatorId && cc.WorkspaceId == selectedWorkspaceId).ToList();
            foreach (var cc in ClassCourses)
            {
                var timetableConstraints = _context.TimetableConstraints.Where(tc => tc.ClassCourseId == cc.ClassCourseId).ToList();
                foreach (var tc in timetableConstraints)
                {
                    _context.TimetableConstraints.Remove(tc);
                }
            }
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
