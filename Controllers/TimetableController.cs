using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using GWOTimetable.Services;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class TimetableController : Controller
    {
        private readonly Db12026Context _context;
        private readonly GWOSchedulerService _schedulerService;

        public TimetableController()
        {
            _context = new Db12026Context();
            _schedulerService = new GWOSchedulerService(_context);
        }


        public async Task<IActionResult> Management()
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            //authentication kontrolü eklenebilir

            var ws = _context.Workspaces.Where(w => w.WorkspaceId == selectedWorkspaceId);
            var timetables = _context.Timetables.Include(t => t.TimetableState).Where(t => t.WorkspaceId == selectedWorkspaceId).ToList();

            ViewBag.ActiveTabId = "TimetableManagement";
            return View(timetables);
        }
        
        public async Task<IActionResult> Details(int timetableId, string viewType = "Educator", int? itemId = null)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
            
            // Get the timetable information
            var timetable = await _context.Timetables
                .Include(t => t.TimetableState)
                .FirstOrDefaultAsync(t => t.TimetableId == timetableId && t.WorkspaceId == selectedWorkspaceId);
                
            if (timetable == null)
            {
                return NotFound();
            }
            
            // Get all educators and classes for the dropdowns
            var educators = await _context.Educators
                .Where(e => e.WorkspaceId == selectedWorkspaceId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new { e.EducatorId, Name = $"{e.Title} {e.FirstName} {e.LastName}".Trim() })
                .ToListAsync();
                
            var classes = await _context.Classes
                .Where(c => c.WorkspaceId == selectedWorkspaceId)
                .OrderBy(c => c.ClassName)
                .Select(c => new { c.ClassId, c.ClassName })
                .ToListAsync();
                
            // Get placements based on the viewType and itemId
            var placements = new List<TimetablePlacement>();
            
            if (viewType == "Educator" && itemId.HasValue)
            {
                var educatorName = educators.FirstOrDefault(e => e.EducatorId == itemId)?.Name;
                if (educatorName != null)
                {
                    placements = await _context.TimetablePlacements
                        .Where(p => p.WorkspaceId == selectedWorkspaceId && p.EducatorFullName == educatorName)
                        .OrderBy(p => p.DayId) // DayOfWeek yerine DayId'ye göre sıralama
                        .ThenBy(p => p.LessonNumber)
                        .ToListAsync();
                }
            }
            else if (viewType == "Class" && itemId.HasValue)
            {
                var className = classes.FirstOrDefault(c => c.ClassId == itemId)?.ClassName;
                if (className != null)
                {
                    placements = await _context.TimetablePlacements
                        .Where(p => p.WorkspaceId == selectedWorkspaceId && p.ClassName == className)
                        .OrderBy(p => p.DayId) // DayOfWeek yerine DayId'ye göre sıralama
                        .ThenBy(p => p.LessonNumber)
                        .ToListAsync();
                }
            }
            
            // Prepare view data
            ViewBag.TimetableId = timetableId;
            ViewBag.Timetable = timetable;
            ViewBag.ViewType = viewType;
            ViewBag.ItemId = itemId;
            ViewBag.Educators = educators;
            ViewBag.Classes = classes;
            ViewBag.ActiveTabId = "TimetableManagement";
            
            return View(placements);
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] TimetableDeleteModel model)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                // Get the timetable and verify it belongs to the current workspace
                var timetable = await _context.Timetables
                    .FirstOrDefaultAsync(t => t.TimetableId == model.TimetableId && t.WorkspaceId == selectedWorkspaceId);

                if (timetable == null)
                {
                    return Json(new { success = false, message = "Timetable not found." });
                }

                // Start a transaction to ensure both timetable and its placements are deleted atomically
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // First delete all placements associated with this timetable
                        var placements = await _context.TimetablePlacements
                            .Where(tp => tp.TimetableId == model.TimetableId)
                            .ToListAsync();

                        if (placements.Any())
                        {
                            _context.TimetablePlacements.RemoveRange(placements);
                            await _context.SaveChangesAsync();
                        }

                        // Then delete the timetable itself
                        _context.Timetables.Remove(timetable);
                        await _context.SaveChangesAsync();

                        // Commit the transaction
                        await transaction.CommitAsync();

                        return Json(new { success = true });
                    }
                    catch (Exception ex)
                    {
                        // Roll back the transaction if any error occurs
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "An error occurred while deleting the timetable." });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while processing your request." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessTimetable([FromBody] TimetableProcessModel model)
        {
            try
            {
                // Check if timetable exists
                var timetable = await _context.Timetables.FindAsync(model.TimetableId);
                if (timetable == null)
                {
                    return NotFound(new { success = false, message = "Timetable not found" });
                }
                
                // Verify user has access to this timetable (workspace check)
                Guid workspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
                if (timetable.WorkspaceId != workspaceId)
                {
                    return Unauthorized(new { success = false, message = "You do not have permission to process this timetable" });
                }
                
                // Start scheduling in the background (fire and forget)
                // This is important since the optimization may take a long time
                _ = Task.Run(async () =>
                {
                    await _schedulerService.RunSchedulerAsync(model.TimetableId);
                });
                
                return Ok(new { success = true, message = "Timetable scheduling process has started. You can check its status later." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> TimetableStatus(int timetableId)
        {
            try
            {
                var timetable = await _context.Timetables
                    .Include(t => t.TimetableState)
                    .FirstOrDefaultAsync(t => t.TimetableId == timetableId);
                    
                if (timetable == null)
                {
                    return NotFound(new { success = false, message = "Timetable not found" });
                }
                
                // Verify user has access to this timetable (workspace check)
                Guid workspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
                if (timetable.WorkspaceId != workspaceId)
                {
                    return Unauthorized(new { success = false, message = "You do not have permission to view this timetable" });
                }
                
                return Ok(new { 
                    success = true, 
                    state = timetable.TimetableState.State,
                    stateId = timetable.TimetableStateId,
                    updatedAt = timetable.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> GetItemOptions(string viewType, int timetableId)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
                
                // Verify timetable exists and belongs to user's workspace
                var timetable = await _context.Timetables
                    .FirstOrDefaultAsync(t => t.TimetableId == timetableId && t.WorkspaceId == selectedWorkspaceId);
                    
                if (timetable == null)
                {
                    return NotFound(new { success = false, message = "Timetable not found" });
                }
                
                if (viewType == "Educator")
                {
                    var educators = await _context.Educators
                        .Where(e => e.WorkspaceId == selectedWorkspaceId)
                        .OrderBy(e => e.LastName)
                        .ThenBy(e => e.FirstName)
                        .Select(e => new { 
                            educatorId = e.EducatorId, 
                            name = $"{e.Title} {e.FirstName} {e.LastName}".Trim() 
                        })
                        .ToListAsync();
                    
                    return Json(educators);
                }
                else if (viewType == "Class")
                {
                    var classes = await _context.Classes
                        .Where(c => c.WorkspaceId == selectedWorkspaceId)
                        .OrderBy(c => c.ClassName)
                        .Select(c => new { 
                            classId = c.ClassId, 
                            className = c.ClassName 
                        })
                        .ToListAsync();
                    
                    return Json(classes);
                }
                
                return BadRequest(new { success = false, message = "Invalid view type" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateTimetable([FromBody] TimetableCreateModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Tag))
                {
                    return BadRequest(new { success = false, message = "Timetable tag is required" });
                }
                
                Guid workspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
                
                var timetable = new Timetable
                {
                    WorkspaceId = workspaceId,
                    Tag = model.Tag,
                    Description = model.Description,
                    TimetableStateId = 1, // Setting to 1 as requested
                    CreatedAt = DateTime.Now
                };
                
                _context.Timetables.Add(timetable);
                await _context.SaveChangesAsync();
                
                return Ok(new { success = true, message = "Timetable has been added to the queue and will be scheduled soon" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }
    }
    
    public class TimetableCreateModel
    {
        public string Tag { get; set; }
        public string Description { get; set; }
    }
    
    public class TimetableProcessModel
    {
        public int TimetableId { get; set; }
    }

    public class TimetableDeleteModel
    {
        public int TimetableId { get; set; }
    }
}

