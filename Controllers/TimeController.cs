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
    public class TimeController : Controller
    {
        private readonly Db12026Context _context;

        public TimeController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var ws = _context.Workspaces.Where(w => w.WorkspaceId == selectedWorkspaceId)
            .Include(w => w.Days)
            .Include(w => w.Lessons).FirstOrDefault();

            ViewBag.ActiveTabId = "TimeManagement";
            return View(ws);
        }

        [HttpPost]
        public async Task<IActionResult> AddLesson([FromBody] Lesson newLesson)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            if (string.IsNullOrWhiteSpace(newLesson.StartTime))
            {
                return BadRequest(new { message = "Starting time cannot be empty!" });
            }

            if (string.IsNullOrWhiteSpace(newLesson.EndTime))
            {
                return BadRequest(new { message = "Ending time cannot be empty!" });
            }

            var timeRegex = new Regex(@"^([0-9]{1,2}):([0-9]{1,2})$");
            if (!timeRegex.IsMatch(newLesson.StartTime))
            {
                return BadRequest(new { message = "Starting time must be in 99:99 format!" });
            }

            if (!timeRegex.IsMatch(newLesson.EndTime))
            {
                return BadRequest(new { message = "Ending time must be in 99:99 format!" });
            }

            var lessons = _context.Lessons.Where(l => l.WorkspaceId == selectedWorkspaceId).ToList();

            if (lessons.Count > 0)
            {
                var lastLesson = lessons.OrderByDescending(l => l.StartTime).First();
                if (newLesson.StartTime.CompareTo(lastLesson.EndTime) < 0)
                {
                    return BadRequest(new { message = "New lesson's start time must be greater than the last lesson's end time!" });
                }
            }

            if (newLesson.EndTime.CompareTo(newLesson.StartTime) <= 0)
            {
                return BadRequest(new { message = "End time cannot be equal to or earlier than start time!" });
            }

            newLesson.WorkspaceId = selectedWorkspaceId;
            newLesson.LessonNumber = (byte)(lessons.Count + 1);
            _context.Lessons.Add(newLesson);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateEducator([FromBody] Educator educator)
        {
            if (string.IsNullOrWhiteSpace(educator.FirstName) || string.IsNullOrWhiteSpace(educator.LastName))
            {
                return BadRequest(new { message = "Firstname and lastname can not be empty!" });
            }

            if (educator.FirstName.Length > 50)
            {
                return BadRequest(new { message = "Firstname cannot be longer than 50 characters!" });
            }

            if (educator.LastName.Length > 50)
            {
                return BadRequest(new { message = "Lastname cannot be longer than 50 characters!" });
            }

            if (educator.Title.Length > 20)
            {
                return BadRequest(new { message = "Title cannot be longer than 20 characters!" });
            }

            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(educator.FirstName, nameRegex) || !Regex.IsMatch(educator.LastName, nameRegex))
            {
                return BadRequest(new { message = "Firstname and lastname must contain at least one letter and can contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(educator.Title))
            {
                return BadRequest(new { message = "Title cannot be empty!" });
            }

            if (!Regex.IsMatch(educator.Title, @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ\.]+$"))
            {
                return BadRequest(new { message = "Title must contain only letters and dots!" });
            }

            if (string.IsNullOrWhiteSpace(educator.ShortName))
            {
                return BadRequest(new { message = "Shortname cannot be empty!" });
            }

            if (educator.ShortName.Length > 10)
            {
                return BadRequest(new { message = "Shortname cannot be longer than 10 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (educator.EducatorId != null && _context.Educators.Any(e => e.ShortName == educator.ShortName && e.WorkspaceId == selectedWorkspaceId && e.EducatorId != educator.EducatorId))
            {
                return BadRequest(new { message = "Shortname is already in use in this workspace!" });
            }

            var codeRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (educator.ShortName.Contains(" ") || !Regex.IsMatch(educator.ShortName.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Shortname must contain only letters and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(educator.Email))
            {
                return BadRequest(new { message = "Email cannot be empty!" });
            }

            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(educator.Email, emailRegex))
            {
                return BadRequest(new { message = "Email is not valid!" });
            }

            if (educator.EducatorId != null && _context.Educators.Any(e => e.Email == educator.Email && e.WorkspaceId == selectedWorkspaceId && e.EducatorId != educator.EducatorId))
            {
                return BadRequest(new { message = "Shortname is already in use in this workspace!" });
            }

            var oldEducator = await _context.Educators.FirstOrDefaultAsync(c => c.EducatorId == educator.EducatorId);
            oldEducator.UpdatedAt = DateTime.Now;
            oldEducator.ShortName = educator.ShortName.Trim().ToUpper();
            oldEducator.FirstName = Utilities.ToProperCase(educator.FirstName.Trim());
            oldEducator.LastName = Utilities.ToProperCase(educator.LastName.Trim());
            oldEducator.Email = educator.Email.ToLower().Trim();
            oldEducator.Title = Utilities.ToProperCase(educator.Title.Trim());


            _context.Entry(oldEducator).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteLesson([FromBody] Lesson deleteLesson)
        {

            if (deleteLesson == null)
            {
                return BadRequest(new { message = "Educator cannot be empty!" });
            }

            var lesson = await _context.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.LessonId == deleteLesson.LessonId);

            if (lesson == null)
            {
                return NotFound(new { message = "Lesson not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (lesson.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Lesson cannot be deleted!" });
            }

            if (lesson.LessonId != _context.Lessons
                .AsNoTracking()
                .Where(l => l.WorkspaceId == selectedWorkspaceId)
                .OrderByDescending(l => l.LessonId)
                .First().LessonId)
            {
                return BadRequest(new { message = "This lesson cannot be deleted!" });
            }


            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

