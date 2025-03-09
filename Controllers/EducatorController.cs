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

        public IActionResult Details(int educatorId)
        {
            if (educatorId == null)
            {
                return RedirectToAction("Management", "Educator");
            }

            var educator = _context.Educators.FirstOrDefault(c => c.EducatorId == educatorId);
            if (educator == null)
            {
                return RedirectToAction("Management", "Educator");
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (educator.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Educator is not reachable!" });
            }

            var workspace = _context.Workspaces
                       .Include(w => w.Educators.Where(c => c.EducatorId == educatorId)).ThenInclude(c => c.ClassCourses)
                       .Include(w => w.Courses)
                       .Include(w => w.Classes)
                       .Include(w => w.Classrooms)
                       .Include(w => w.Days)
                       .Include(w => w.Lessons)
                       .FirstOrDefault(w => w.WorkspaceId == selectedWorkspaceId);

            ViewBag.ActiveTabId = "EducatorDetails";
            return View(workspace);
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

            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(newEducator.FirstName, nameRegex) || !Regex.IsMatch(newEducator.LastName, nameRegex))
            {
                return BadRequest(new { message = "Firstname and lastname must contain at least one letter and can contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(newEducator.Title))
            {
                return BadRequest(new { message = "Title cannot be empty!" });
            }

            if (!Regex.IsMatch(newEducator.Title, @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ\.]+$"))
            {
                return BadRequest(new { message = "Title must contain only letters and dots!" });
            }

            if (string.IsNullOrWhiteSpace(newEducator.ShortName))
            {
                return BadRequest(new { message = "Shortname cannot be empty!" });
            }

            if (newEducator.ShortName.Length > 10)
            {
                return BadRequest(new { message = "Shortname cannot be longer than 10 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Educators.Any(c => c.ShortName == newEducator.ShortName && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Shortname is already in use in this workspace!" });
            }

            var codeRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (newEducator.ShortName.Contains(" ") || !Regex.IsMatch(newEducator.ShortName.Trim(), codeRegex))
            {
                return BadRequest(new { message = "Shortname must contain only letters and cannot contain spaces!" });
            }

            if (string.IsNullOrWhiteSpace(newEducator.Email))
            {
                return BadRequest(new { message = "Email cannot be empty!" });
            }

            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
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

