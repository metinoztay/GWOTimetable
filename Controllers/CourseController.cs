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

        [HttpPost]
        public async Task<IActionResult> NewCourse([FromBody] Course newCourse)
        {
            if (string.IsNullOrWhiteSpace(newCourse.CourseCode))
            {
                return BadRequest(new { message = "Code can not be empty!" });
            }

            var codeRegex = @"^[a-zA-Z0-9]";
            if (!Regex.IsMatch(newCourse.CourseCode.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Code must contain only letters and numbers!" });
            }
            if (string.IsNullOrWhiteSpace(newCourse.CourseName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\s]+$";
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



            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Courses.Any(c => c.CourseCode == newCourse.CourseCode && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Code is already in use in this workspace!" });
            }

            newCourse.CreatedAt = DateTime.Now;
            newCourse.CourseCode = newCourse.CourseCode.ToUpper();
            newCourse.CourseName = Utilities.ToProperCase(newCourse.CourseName.Trim());
            newCourse.WorkspaceId = selectedWorkspaceId;
            _context.Courses.Add(newCourse);
            await _context.SaveChangesAsync();
            return Ok();
        }


        [HttpPost]
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

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

