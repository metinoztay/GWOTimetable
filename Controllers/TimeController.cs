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
                return BadRequest(new { message = "End time cannot be earlier than start time!" });
            }

            newLesson.WorkspaceId = selectedWorkspaceId;
            newLesson.LessonNumber = (byte)(lessons.Count + 1);
            _context.Lessons.Add(newLesson);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateDays([FromBody] List<Day> days)
        {

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            var lessons = _context.Lessons.Where(l => l.WorkspaceId == selectedWorkspaceId).ToList();

            foreach (var day in days)
            {
                if (day.DayOfWeek == null)
                {
                    return BadRequest(new { message = "Day of week cannot be null!" });
                }
                if (string.IsNullOrWhiteSpace(day.ShortName))
                {
                    return BadRequest(new { message = "Short name cannot be null or empty!" });
                }
                if (day.DayOfWeek.Length > 20)
                {
                    return BadRequest(new { message = "Day of week cannot be longer than 20 characters!" });
                }
                if (day.ShortName.Length > 5)
                {
                    return BadRequest(new { message = "Short name cannot be longer than 5 characters!" });
                }
                if (!Regex.IsMatch(day.DayOfWeek, @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$"))
                {
                    return BadRequest(new { message = "Day of week can only contain letters and spaces!" });
                }
                if (!Regex.IsMatch(day.ShortName, @"^[a-zA-ZçÇğĞıİöÖşŞüÜ]+$"))
                {
                    return BadRequest(new { message = "Short name can only contain letters!" });
                }
                if (day.LessonCount < 0)
                {
                    return BadRequest(new { message = "Lesson count cannot be less than 0!" });
                }
                if (day.LessonCount > lessons.Count)
                {
                    return BadRequest(new { message = "Lesson count cannot be greater than the number of lessons!" });
                }
                if (!_context.Days.Any(d => d.DayId == day.DayId && d.WorkspaceId == selectedWorkspaceId))
                {
                    return BadRequest(new { message = "Day does not belong to selected workspace!" });
                }
            }

            foreach (var day in days)
            {
                day.WorkspaceId = selectedWorkspaceId;
                day.DayOfWeek = Utilities.ToProperCase(day.DayOfWeek);
                day.ShortName = day.ShortName.ToUpper();
                _context.Entry(day).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateLessons([FromBody] List<Lesson> lessons)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            lessons = lessons.OrderBy(l => l.LessonNumber).ToList();


            string lastEndTime = "00:00";

            foreach (var lesson in lessons)
            {
                if (string.IsNullOrWhiteSpace(lesson.StartTime))
                {
                    return BadRequest(new { message = "Starting time cannot be empty!" });
                }

                if (string.IsNullOrWhiteSpace(lesson.EndTime))
                {
                    return BadRequest(new { message = "Ending time cannot be empty!" });
                }

                var timeRegex = new Regex(@"^([0-9]{1,2}):([0-9]{1,2})$");
                if (!timeRegex.IsMatch(lesson.StartTime))
                {
                    return BadRequest(new { message = "Starting time must be in 99:99 format!" });
                }

                if (!timeRegex.IsMatch(lesson.EndTime))
                {
                    return BadRequest(new { message = "Ending time must be in 99:99 format!" });
                }

                if (lesson.EndTime.CompareTo(lesson.StartTime) <= 0)
                {
                    return BadRequest(new { message = "End time cannot be earlier than start time!" });
                }

                if (lesson.StartTime.CompareTo(lastEndTime) < 0)
                {
                    return BadRequest(new { message = "Lesson's start time must be greater than the previous lesson's end time!" });
                }

                lastEndTime = lesson.EndTime;
            }

            foreach (var lesson in lessons)
            {
                lesson.WorkspaceId = selectedWorkspaceId;
                _context.Entry(lesson).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

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

            var days = _context.Days.Where(d => d.WorkspaceId == selectedWorkspaceId).ToList();
            foreach (var day in days)
            {
                if (day.LessonCount == lesson.LessonNumber)
                {
                    day.LessonCount -= 1;
                    _context.Entry(day).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                }
            }


            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

