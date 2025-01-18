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
    public class ClassRoomController : Controller
    {
        private readonly Db12026Context _context;

        public ClassRoomController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var classRooms = _context.ClassRooms.Where(c => c.WorkspaceId == selectedWorkspaceId).ToList();


            ViewBag.ActiveTabId = "ClassRoomManagement";
            return View(classRooms);
        }

        public IActionResult Details(int classroomId)
        {
            if (classroomId == null)
            {
                return RedirectToAction("Management", "ClassRoom");
            }

            var classroom = _context.ClassRooms.FirstOrDefault(c => c.ClassRoomId == classroomId);
            if (classroom == null)
            {
                return RedirectToAction("Management", "ClassRoom");
            }

            ViewBag.ActiveTabId = "ClassRoomDetails";
            return View(classroom);
        }

        [HttpPost]
        public async Task<IActionResult> NewClassRoom([FromBody] ClassRoom newClassRoom)
        {
            if (string.IsNullOrWhiteSpace(newClassRoom.ClassRoomName))
            {
                return BadRequest(new { message = "Name can not be empty!" });
            }

            if (newClassRoom.ClassRoomName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            var classNameRegex = @"^[a-zA-Z0-9\s]";
            if (!Regex.IsMatch(newClassRoom.ClassRoomName.Trim(), classNameRegex))
            {
                return BadRequest(new { message = "Class name must contain only letters, numbers." });
            }


            if (newClassRoom.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (_context.Classes.Any(c => c.ClassName == newClassRoom.ClassRoomName && c.WorkspaceId == selectedWorkspaceId))
            {
                return BadRequest(new { message = "ClassRoom name is already in use in this workspace!" });
            }

            newClassRoom.CreatedAt = DateTime.Now;
            newClassRoom.ClassRoomName = Utilities.ToProperCase(newClassRoom.ClassRoomName.Trim());
            newClassRoom.WorkspaceId = selectedWorkspaceId;
            newClassRoom.Description = newClassRoom.Description.Trim();
            _context.ClassRooms.Add(newClassRoom);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateClassRoom([FromBody] ClassRoom classroom)
        {
            if (classroom.ClassRoomName.Length > 50)
            {
                return BadRequest(new { message = "Name cannot be longer than 50 characters!" });
            }

            if (classroom.Description?.Length > 250)
            {
                return BadRequest(new { message = "Description cannot be longer than 250 characters!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (classroom.ClassRoomId != null && _context.ClassRooms.Any(c => c.ClassRoomName == classroom.ClassRoomName && classroom.WorkspaceId == selectedWorkspaceId && c.ClassRoomId != classroom.ClassRoomId))
            {
                return BadRequest(new { message = "Name is already in use in this workspace!" });
            }

            if (string.IsNullOrWhiteSpace(classroom.ClassRoomName))
            {
                return BadRequest(new { message = "Name cannot be empty!" });
            }

            var nameRegex = @"^[a-zA-Z\s]";
            if (!Regex.IsMatch(classroom.ClassRoomName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "Name must contain only letters!" });
            }

            var oldClassRoom = await _context.ClassRooms.FirstOrDefaultAsync(c => c.ClassRoomId == classroom.ClassRoomId);
            oldClassRoom.UpdatedAt = DateTime.Now;
            oldClassRoom.ClassRoomName = Utilities.ToProperCase(classroom.ClassRoomName.Trim());
            oldClassRoom.Description = classroom.Description.Trim();

            _context.Entry(oldClassRoom).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteClassRoom([FromBody] ClassRoom deleteClassRoom)
        {
            if (deleteClassRoom == null)
            {
                return BadRequest(new { message = "ClassRoom cannot be empty!" });
            }

            deleteClassRoom = await _context.ClassRooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassRoomId == deleteClassRoom.ClassRoomId);

            if (deleteClassRoom == null)
            {
                return NotFound(new { message = "ClassRoom not found!" });
            }

            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            if (deleteClassRoom.WorkspaceId != selectedWorkspaceId)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Unauthorized(new { message = "You are not authorized to delete this classroom!" });
            }

            _context.ClassRooms.Remove(deleteClassRoom);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}

