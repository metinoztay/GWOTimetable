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
    public class ClassroomController : Controller
    {
        private readonly Db12026Context _context;

        public ClassroomController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var classrooms = _context.Classrooms.Where(c => c.WorkspaceId == selectedWorkspaceId).ToList();


            ViewBag.ActiveTabId = "ClassroomManagement";
            return View(classrooms);
        }

        public IActionResult Details(int classroomId)
        {
            if (classroomId == null)
            {
                return RedirectToAction("Management", "Classroom");
            }

            var classroom = _context.Classrooms.FirstOrDefault(c => c.ClassroomId == classroomId);
            if (classroom == null)
            {
                return RedirectToAction("Management", "Classroom");
            }

            ViewBag.ActiveTabId = "ClassroomDetails";
            return View(classroom);
        }

        [HttpPost]
        public async Task<IActionResult> NewClassroom([FromBody] Classroom newClassroom)
        {
            if (string.IsNullOrWhiteSpace(newClassroom.ClassroomName))
            {
                return BadRequest(new { message = "Name can not be empty!" });
            }

            if (newClassroom.ClassroomName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            var classNameRegex = @"^[a-zA-Z0-9\s]";
            if (!Regex.IsMatch(newClassroom.ClassroomName.Trim(), classNameRegex))
            {
                return BadRequest(new { message = "Class name must contain only letters, numbers." });
            }


            if (newClassroom.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Classes.Any(c => c.ClassName == newClassroom.ClassroomName && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "Classroom name is already in use in this workspace!" });
            }

            newClassroom.CreatedAt = DateTime.Now;
            newClassroom.ClassroomName = Utilities.ToProperCase(newClassroom.ClassroomName.Trim());
            newClassroom.WorkspaceId = selectedWorkspaceId;
            newClassroom.Description = newClassroom.Description.Trim();
            _context.Classrooms.Add(newClassroom);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateClassroom([FromBody] Classroom classroom)
        {
            if (classroom.ClassroomName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            if (classroom.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (classroom.ClassroomId != null && _context.Classrooms.Any(c => c.ClassroomName == classroom.ClassroomName && classroom.WorkspaceId == selectedWorkspaceId && c.ClassroomId != classroom.ClassroomId))
            {
                return BadRequest(new { message = "Name is already in use in this workspace!" });
            }

            if (string.IsNullOrWhiteSpace(classroom.ClassroomName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\s]";
            if (!Regex.IsMatch(classroom.ClassroomName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "Name must contain only letters!" });
            }

            var oldClassroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.ClassroomId == classroom.ClassroomId);
            oldClassroom.UpdatedAt = DateTime.Now;
            oldClassroom.ClassroomName = Utilities.ToProperCase(classroom.ClassroomName.Trim());
            oldClassroom.Description = classroom.Description.Trim();

            _context.Entry(oldClassroom).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteClassroom([FromBody] Classroom deleteClassroom)
        {
            if (deleteClassroom == null)
            {
                return BadRequest(new { message = "Classroom cannot be empty!" });
            }

            var classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassroomId == deleteClassroom.ClassroomId);

            if (classroom == null)
            {
                return NotFound(new { message = "Classroom not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (classroom.WorkspaceId != selectedWorkspaceId)
            {
                return BadRequest(new { message = "Classroom cannot be deleted!" });
            }

            _context.Classrooms.Remove(classroom);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

