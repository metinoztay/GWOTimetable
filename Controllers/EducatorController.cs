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
    public class EducatorController : Controller
    {
        private readonly Db12026Context _context;

        public EducatorController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var educators = _context.Educators.Where(c => c.WorkspaceId == selectedWorkspaceId).ToList();


            ViewBag.ActiveTabId = "EducatorManagement";
            return View(educators);
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
        public async Task<IActionResult> NewEducator([FromBody] Educator newEducator)
        {

            if (string.IsNullOrWhiteSpace(newEducator.FirstName) || string.IsNullOrWhiteSpace(newEducator.LastName))
            {
                return BadRequest(new { message = "Firstname and lastname can not be empty!" });
            }

            if (newEducator.FirstName.Length > 50)
            {
                return BadRequest(new { message = "Firstname cannot be longer than 50 characters!" });
            }

            if (newEducator.LastName.Length > 50)
            {
                return BadRequest(new { message = "Lastname cannot be longer than 50 characters!" });
            }

            if (newEducator.Title.Length > 20)
            {
                return BadRequest(new { message = "Title cannot be longer than 20 characters!" });
            }



            var nameRegex = @"^[a-zA-Z\.]+$";
            if (!Regex.IsMatch(newEducator.FirstName, nameRegex) || !Regex.IsMatch(newEducator.LastName, nameRegex) || !Regex.IsMatch(newEducator.Title, nameRegex))
            {
                return BadRequest(new { message = "Firstname, lastname and title can only contain letters and periods!" });
            }

            if (string.IsNullOrWhiteSpace(newEducator.ShortName))
            {
                return BadRequest(new { message = "Shortname cannot be empty!" });
            }

            if (newEducator.ShortName.Length > 20)
            {
                return BadRequest(new { message = "Shortname cannot be longer than 20 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Educators.Any(c => c.ShortName == newEducator.ShortName && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Shortname is already in use in this workspace!" });
            }

            var codeRegex = @"^[a-zA-Z]";
            if (newEducator.ShortName.Contains(" ") || !Regex.IsMatch(newEducator.ShortName.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Shortname must contain only letters and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(newEducator.Email))
            {
                return BadRequest(new { message = "Email cannot be empty!" });
            }

            var emailRegex = @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$";
            if (!Regex.IsMatch(newEducator.Email, emailRegex))
            {
                return BadRequest(new { message = "Email is not valid!" });
            }

            if (_context.Educators.Any(c => c.Email == newEducator.Email && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Email is already in use in this workspace!" });
            }

            newEducator.CreatedAt = DateTime.Now;
            newEducator.Title = Utilities.ToProperCase(newEducator.Title.Trim());
            newEducator.FirstName = Utilities.ToProperCase(newEducator.FirstName.Trim());
            newEducator.LastName = Utilities.ToProperCase(newEducator.LastName.Trim());
            newEducator.WorkspaceId = selectedWorkspaceId;
            _context.Educators.Add(newEducator);
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
        public async Task<IActionResult> DeleteEducator([FromBody] Educator deleteEducator)
        {

            if (deleteEducator == null)
            {
                return BadRequest(new { message = "Educator cannot be empty!" });
            }

            var educator = await _context.Educators
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.EducatorId == deleteEducator.EducatorId);

            if (educator == null)
            {
                return NotFound(new { message = "Educator not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (educator.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Educator cannot be deleted!" });
            }

            _context.Educators.Remove(educator);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

