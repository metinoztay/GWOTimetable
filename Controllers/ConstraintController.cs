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
        public async Task<IActionResult> GetConstraintsForEducator([FromBody] EducatorConstraintDTO educatorData)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            var workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.TimetableConstraints.Where(tc => tc.ClassCourse.EducatorId == educatorData.EducatorId))
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == educatorData.EducatorId))
                .Include(w => w.ClassConstraints)
                .Include(w => w.ClassroomConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);

            if (workspace == null)
                return NotFound();

            ViewData["SelectedClassCourseId"] = educatorData.ClassCourseId;
            ViewData["SelectedEducatorId"] = educatorData.EducatorId;

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

        [HttpPost]
        public async Task<IActionResult> GetConstraintsForClass([FromBody] ClassConstraintDTO classData)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            // First get the selected class course to find the educator
            int? educatorId = null;
            if (classData.ClassCourseId > 0)
            {
                var selectedClassCourse = await _context.ClassCourses
                    .FirstOrDefaultAsync(cc => cc.ClassCourseId == classData.ClassCourseId);
                if (selectedClassCourse != null)
                {
                    educatorId = selectedClassCourse.EducatorId;
                }
            }

            var workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.ClassCourses.Where(c => c.ClassId == classData.ClassId))
                    .ThenInclude(c => c.TimetableConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .Include(w => w.ClassConstraints.Where(c => c.ClassId == classData.ClassId))
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == educatorId))
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);

            if (workspace == null)
                return NotFound();

            ViewData["SelectedClassCourseId"] = classData.ClassCourseId;
            ViewData["SelectedEducatorId"] = educatorId;

            return PartialView("_ConstraintsClass", workspace);
        }

        [HttpPost]
        public async Task<IActionResult> AddConstraintForClass([FromBody] ClassConstraintDTO classData)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                if (classData.ClassCourseId == 0)
                {
                    ClassConstraint classConstraint = new ClassConstraint();
                    classConstraint.WorkspaceId = selectedWorkspaceId;
                    classConstraint.ClassId = classData.ClassId;
                    classConstraint.DayId = classData.DayId;
                    classConstraint.LessonId = classData.LessonId;
                    classConstraint.IsPlaceable = false;
                    _context.ClassConstraints.Add(classConstraint);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    TimetableConstraint tc = new TimetableConstraint();
                    tc.WorkspaceId = selectedWorkspaceId;
                    tc.DayId = classData.DayId;
                    tc.LessonId = classData.LessonId;
                    tc.ClassCourseId = classData.ClassCourseId;
                    _context.TimetableConstraints.Add(tc);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveConstraintForClass([FromBody] ClassConstraintDTO classData)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                if (classData.ClassCourseId == 0)
                {
                    var classConstraint = await _context.ClassConstraints
                        .FirstOrDefaultAsync(cc => 
                            cc.WorkspaceId == selectedWorkspaceId && 
                            cc.ClassId == classData.ClassId &&
                            cc.DayId == classData.DayId && 
                            cc.LessonId == classData.LessonId);

                    if (classConstraint != null)
                    {
                        _context.ClassConstraints.Remove(classConstraint);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    var timetableConstraint = await _context.TimetableConstraints
                        .FirstOrDefaultAsync(tc => 
                            tc.WorkspaceId == selectedWorkspaceId && 
                            tc.DayId == classData.DayId && 
                            tc.LessonId == classData.LessonId && 
                            tc.ClassCourseId == classData.ClassCourseId);

                    if (timetableConstraint != null)
                    {
                        _context.TimetableConstraints.Remove(timetableConstraint);
                        await _context.SaveChangesAsync();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearConstraints([FromBody] ClassConstraintDTO classData)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                var constraints = await _context.TimetableConstraints
                    .Include(tc => tc.ClassCourse)
                    .Where(tc => tc.WorkspaceId == selectedWorkspaceId && 
                           tc.ClassCourse.ClassId == classData.ClassId)
                    .ToListAsync();

                _context.TimetableConstraints.RemoveRange(constraints);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class ClassConstraintDTO
        {
            public int ClassId { get; set; }
            public int ClassCourseId { get; set; }
            public int DayId { get; set; }
            public int LessonId { get; set; }
        }
    }
}
