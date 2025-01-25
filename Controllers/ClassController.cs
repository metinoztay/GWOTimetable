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

        public IActionResult Details(int classId)
        {
            if (classId == null)
            {
                return RedirectToAction("Management", "Class");
            }

            var c = _context.Classes.FirstOrDefault(c => c.ClassId == classId);

            if (c == null)
            {
                return RedirectToAction("Management", "Class");
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (c.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Class is not reachable!" });
            }

            var workspace = _context.Workspaces
            .Include(w => w.Classes.Where(c => c.ClassId == classId)).ThenInclude(c => c.ClassCourses)
            .Include(w => w.Courses)
            .Include(w => w.Educators)
            .Include(w => w.Classrooms)
            .FirstOrDefault(w => w.WorkspaceId == selectedWorkspaceId);

            ViewBag.ActiveTabId = "ClassDetails";
            return View(workspace);
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

            var classNameRegex = @"^[a-zA-Z0-9\sçÇğĞıİöÖşŞüÜ]+$";
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
        public async Task<IActionResult> UpdateClass([FromBody] Class c)
        {
            if (c.ClassName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            if (c.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (c.ClassId != null && _context.Classes.Any(cl => cl.ClassName == c.ClassName && c.WorkspaceId == selectedWorkspaceId && cl.ClassId != c.ClassId))
            {
                return BadRequest(new { message = "Name is already in use in this workspace!" });
            }

            if (string.IsNullOrWhiteSpace(c.ClassName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z0-9\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(c.ClassName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "Name must contain only letters and numbers!" });
            }

            var oldClass = await _context.Classes.FirstOrDefaultAsync(cl => cl.ClassId == c.ClassId);
            oldClass.UpdatedAt = DateTime.Now;
            oldClass.ClassName = Utilities.ToProperCase(c.ClassName.Trim());
            oldClass.Description = c.Description.Trim();

            _context.Entry(oldClass).State = EntityState.Modified;
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

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (c.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Class cannot be deleted!" });
            }

            _context.Classes.Remove(c);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

