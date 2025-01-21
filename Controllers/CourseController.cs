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
    public class CourseController : Controller
    {
        private readonly Db12026Context _context;

        public CourseController()
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
        public async Task<IActionResult> NewCourse([FromBody] Course newCourse)
        {
            if (newCourse.CourseCode.Length > 15)
            {
                return BadRequest(new { message = "Code cannot be longer than 15 characters!" });
            }

            if (newCourse.CourseName.Length > 100)
            {
                return BadRequest(new { message = "Name cannot be longer than 100 characters!" });
            }

            if (newCourse.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            if (string.IsNullOrWhiteSpace(newCourse.CourseCode))
            {
                return BadRequest(new { message = "Code can not be empty!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Courses.Any(c => c.CourseCode == newCourse.CourseCode && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Code is already in use in this workspace!" });
            }

            var codeRegex = @"^[a-zA-Z0-9]+$";
            if (newCourse.CourseCode.Contains(" ") || !Regex.IsMatch(newCourse.CourseCode.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Code must contain only letters and numbers, and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(newCourse.CourseName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(newCourse.CourseName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "Name must contain only letters!" });
            }

            if (newCourse.WeeklyHourCount == 0)
            {
                return BadRequest(new { message = "Please select weekly hour count!" });
            }

            if (string.IsNullOrWhiteSpace(newCourse.PlacementFormat))
            {
                return BadRequest(new { message = "Please select a placement format!" });
            }

            newCourse.CreatedAt = DateTime.Now;
            newCourse.CourseCode = newCourse.CourseCode.ToUpper();
            newCourse.CourseName = Utilities.ToProperCase(newCourse.CourseName.Trim());
            newCourse.WorkspaceId = selectedWorkspaceId;
            newCourse.Description = newCourse.Description.Trim();
            _context.Courses.Add(newCourse);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateCourse([FromBody] Course course)
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

            var codeRegex = @"^[a-zA-Z0-9]+$";
            if (course.CourseCode.Contains(" ") || !Regex.IsMatch(course.CourseCode.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Code must contain only letters and numbers, and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(course.CourseName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
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
        public async Task<IActionResult> DeleteCourse([FromBody] Course deleteCourse)
        {
            if (deleteCourse == null)
            {
                return BadRequest(new { message = "Course cannot be empty!" });
            }

            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CourseId == deleteCourse.CourseId);

            if (course == null)
            {
                return NotFound(new { message = "Course not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (course.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Course cannot be deleted!" });
            }
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

