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
    public class ClassController : Controller
    {
        private readonly Db12026Context _context;

        public ClassController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var classes = _context.Classes.Where(c => c.WorkspaceId == selectedWorkspaceId).ToList();


            ViewBag.ActiveTabId = "ClassManagement";
            return View(classes);
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

            ViewBag.ActiveTabId = "CourseDetails";
            return View(course);
        }

        [HttpPost]
        public async Task<IActionResult> NewClass([FromBody] Class newClass)
        {
            if (string.IsNullOrWhiteSpace(newClass.ClassName))
            {
                return BadRequest(new { message = "Name can not be empty!" });
            }

            if (newClass.ClassName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            var classNameRegex = @"^[a-zA-Z0-9\s]+$";
            if (!Regex.IsMatch(newClass.ClassName.Trim(), classNameRegex))
            {
                return BadRequest(new { message = "Class name must contain only letters, numbers." });
            }


            if (newClass.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Classes.Any(c => c.ClassName == newClass.ClassName && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Class name is already in use in this workspace!" });
            }

            newClass.CreatedAt = DateTime.Now;
            newClass.ClassName = Utilities.ToProperCase(newClass.ClassName.Trim());
            newClass.WorkspaceId = selectedWorkspaceId;
            newClass.Description = newClass.Description.Trim();
            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateClass([FromBody] Course course)
        {
            if (course.CourseCode.Length > 15)
            {
                return BadRequest(new { message = "Code cannot be longer than 15 characters!" });
            }

            if (course.CourseName.Length > 100)
            {
                return BadRequest(new { message = "Name cannot be longer than 100 characters!" });
            }

            if (course.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            if (string.IsNullOrWhiteSpace(course.CourseCode))
            {
                return BadRequest(new { message = "Code can not be empty!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (course.CourseId != null && _context.Courses.Any(c => c.CourseCode == course.CourseCode && c.WorkspaceId == selectedWorkspaceId && c.CourseId != course.CourseId))
            {
                return BadRequest(new { message = "Code is already in use in this workspace!" });
            }

            var codeRegex = @"^[a-zA-Z0-9]";
            if (course.CourseCode.Contains(" ") || !Regex.IsMatch(course.CourseCode.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Code must contain only letters and numbers, and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(course.CourseName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\s]";
            if (!Regex.IsMatch(course.CourseName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "Name must contain only letters!" });
            }

            if (course.WeeklyHourCount == 0)
            {
                return BadRequest(new { message = "Please select weekly hour count!" });
            }

            if (string.IsNullOrWhiteSpace(course.PlacementFormat))
            {
                return BadRequest(new { message = "Please select a placement format!" });
            }

            var oldCourse = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == course.CourseId);
            oldCourse.UpdatedAt = DateTime.Now;
            oldCourse.CourseCode = course.CourseCode.ToUpper();
            oldCourse.CourseName = Utilities.ToProperCase(course.CourseName.Trim());
            oldCourse.Description = course.Description.Trim();
            oldCourse.WeeklyHourCount = course.WeeklyHourCount;
            oldCourse.PlacementFormat = course.PlacementFormat;

            _context.Entry(oldCourse).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteClass([FromBody] Class deleteClass)
        {
            if (deleteClass == null)
            {
                return BadRequest(new { message = "Class cannot be empty!" });
            }

            var c = await _context.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassId == deleteClass.ClassId);

            if (c == null)
            {
                return NotFound(new { message = "Class not found!" });
            }

            _context.Classes.Remove(c);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

