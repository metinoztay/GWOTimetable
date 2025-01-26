using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.RegularExpressions;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class ClassCourseController : Controller
    {
        private readonly Db12026Context _context;

        public ClassCourseController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var courses = _context.Courses.Where(c => c.WorkspaceId == selectedWorkspaceId).ToList();


            ViewBag.ActiveTabId = "CourseManagement";
            return View(courses);
        }

        public IActionResult Details(int courseId)
        {
            if (courseId == null)
            {
                return RedirectToAction("Management", "Course");
            }

            var course = _context.Courses.FirstOrDefault(c => c.CourseId == courseId);
            if (course == null)
            {
                return RedirectToAction("Management", "Course");
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (course.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Course is not reachable!" });
            }

            ViewBag.ActiveTabId = "CourseDetails";
            return View(course);
        }

        [HttpPost]
        public async Task<IActionResult> NewCourse([FromBody] ClassCourse newCourse)
        {
            if (newCourse.ClassId == null)
            {
                return BadRequest(new { message = "Class id cannot be empty!" });
            }
            if (newCourse.ClassId == 0)
            {
                return BadRequest(new { message = "Please select a class!" });
            }

            if (newCourse == null)
            {
                return BadRequest(new { message = "Course cannot be empty!" });
            }

            if (newCourse.CourseId == 0)
            {
                return BadRequest(new { message = "Please select a course!" });
            }
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            var course = _context.Courses.FirstOrDefault(c => c.CourseId == newCourse.CourseId && c.WorkspaceId == selectedWorkspaceId);
            if (course == null)
            {
                return BadRequest(new { message = "Course not found in this workspace!" });
            }

            if (newCourse.EducatorId == 0)
            {
                return BadRequest(new { message = "Please select an educator!" });
            }

            if (!_context.Educators.Any(e => e.EducatorId == newCourse.EducatorId && e.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Educator not found in this workspace!" });
            }

            if (newCourse.ClassroomId == 0)
            {
                return BadRequest(new { message = "Please select a classroom!" });
            }
            if (!_context.Classrooms.Any(cr => cr.ClassroomId == newCourse.ClassroomId && cr.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Classroom not found in this workspace!" });
            }

            if (_context.ClassCourses.Any(cc => cc.CourseId == newCourse.CourseId && cc.ClassId == newCourse.ClassId))
            {
                return BadRequest(new { message = "Course already added to this class!" });
            }

            if (!_context.Classes.Any(c => c.ClassId == newCourse.ClassId && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Class not found in this workspace!" });
            }

            newCourse.WorkspaceId = selectedWorkspaceId;
            newCourse.CreatedAt = DateTime.Now;
            _context.ClassCourses.Add(newCourse);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateCourse([FromBody] ClassCourse updateCourse)
        {
            if (updateCourse.ClassCourseId == null)
            {
                return BadRequest(new { message = "ClassCourse cannot be empty!" });
            }

            if (updateCourse.ClassId == null)
            {
                return BadRequest(new { message = "Class cannot be empty!" });
            }
            if (updateCourse.ClassId == 0)
            {
                return BadRequest(new { message = "Please select a class!" });
            }

            if (updateCourse == null)
            {
                return BadRequest(new { message = "Course cannot be empty!" });
            }

            if (updateCourse.CourseId == 0)
            {
                return BadRequest(new { message = "Please select a course!" });
            }
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            var course = _context.ClassCourses.FirstOrDefault(c => c.ClassCourseId == updateCourse.ClassCourseId && c.WorkspaceId == selectedWorkspaceId);
            if (course == null)
            {
                return BadRequest(new { message = "Course not found in this workspace!" });
            }

            if (updateCourse.EducatorId == 0)
            {
                return BadRequest(new { message = "Please select an educator!" });
            }

            if (!_context.Educators.Any(e => e.EducatorId == updateCourse.EducatorId && e.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Educator not found in this workspace!" });
            }

            if (updateCourse.ClassroomId == 0)
            {
                return BadRequest(new { message = "Please select a classroom!" });
            }
            if (!_context.Classrooms.Any(cr => cr.ClassroomId == updateCourse.ClassroomId && cr.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Classroom not found in this workspace!" });
            }


            if (!_context.Classes.Any(c => c.ClassId == updateCourse.ClassId && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Class not found in this workspace!" });
            }

            if (_context.ClassCourses.Any(cc => cc.ClassId == updateCourse.ClassId && cc.CourseId == updateCourse.CourseId && cc.ClassCourseId != updateCourse.ClassCourseId))
            {
                return BadRequest(new { message = "Course already added to this class!" });
            }



            course.ClassId = updateCourse.ClassId;
            course.CourseId = updateCourse.CourseId;
            course.ClassroomId = updateCourse.ClassroomId;
            course.EducatorId = updateCourse.EducatorId;
            course.UpdatedAt = DateTime.Now;
            _context.Entry(course).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCourse([FromBody] ClassCourse deleteCourse)
        {
            if (deleteCourse == null)
            {
                return BadRequest(new { message = "Course cannot be empty!" });
            }

            var course = await _context.ClassCourses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassCourseId == deleteCourse.ClassCourseId);

            if (course == null)
            {
                return NotFound(new { message = "Course not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (course.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Course cannot be deleted!" });
            }
            _context.ClassCourses.Remove(course);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

