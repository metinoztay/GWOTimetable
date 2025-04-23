using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class ConstraintController : Controller
    {
        private readonly Db12026Context _context;

        public ConstraintController()
        {
            _context = new Db12026Context();
        }

        [HttpPost]
        public async Task<IActionResult> GetConstraintsForEducator([FromBody] ClassCourse classCourse)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            Workspace workspace = new Workspace();
            if (classCourse.ClassCourseId == 0)
            {
                workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == classCourse.EducatorId))
                .Include(w => w.ClassCourses.Where(c => c.EducatorId == classCourse.EducatorId))
                    .ThenInclude(c => c.TimetableConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
            }
            else
            {
                classCourse = _context.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == classCourse.ClassCourseId);

                workspace = await _context.Workspaces
                .Include(w => w.Days)
                .Include(w => w.Lessons)
                .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == classCourse.EducatorId))
                .Include(w => w.ClassCourses.Where(c => c.EducatorId == classCourse.EducatorId))
                    .ThenInclude(c => c.TimetableConstraints)
                .Include(w => w.ClassCourses)
                    .ThenInclude(c => c.Course)
                .Include(w => w.Courses)
                .Include(w => w.ClassroomConstraints.Where(cr => cr.ClassroomId == classCourse.ClassroomId))
                .Include(w => w.ClassConstraints.Where(c => c.ClassId == classCourse.ClassId))
                .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
            }

            return PartialView("_ConstraintsEducator", workspace);
        }

        [HttpPost]
        public async Task<IActionResult> GetConstraintsForClass([FromBody] ClassCourse classCourse)
        {
            try
            {
                if (classCourse == null || classCourse.ClassId == 0)
                {
                    return BadRequest("Invalid class data");
                }

                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                Workspace workspace = new Workspace();
                if (classCourse.ClassCourseId == 0)
                {
                    workspace = await _context.Workspaces
                    .Include(w => w.Days)
                    .Include(w => w.Lessons)
                    .Include(w => w.ClassConstraints.Where(c => c.ClassId == classCourse.ClassId))
                    .Include(w => w.ClassCourses.Where(c => c.ClassId == classCourse.ClassId))
                        .ThenInclude(c => c.TimetableConstraints)
                    .Include(w => w.ClassCourses)
                        .ThenInclude(c => c.Course)
                    .Include(w => w.Courses)
                    .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
                }
                else
                {
                    var existingClassCourse = await _context.ClassCourses
                        .FirstOrDefaultAsync(cc => cc.ClassCourseId == classCourse.ClassCourseId);

                    if (existingClassCourse == null)
                    {
                        return NotFound("Class course not found");
                    }

                    workspace = await _context.Workspaces
                    .Include(w => w.Days)
                    .Include(w => w.Lessons)
                    .Include(w => w.ClassConstraints.Where(c => c.ClassId == existingClassCourse.ClassId))
                    .Include(w => w.ClassCourses.Where(c => c.ClassId == existingClassCourse.ClassId))
                        .ThenInclude(c => c.TimetableConstraints)
                    .Include(w => w.ClassCourses)
                        .ThenInclude(c => c.Course)
                    .Include(w => w.Courses)
                    .Include(w => w.ClassroomConstraints.Where(cr => cr.ClassroomId == existingClassCourse.ClassroomId))
                    .Include(w => w.EducatorConstraints.Where(e => e.EducatorId == existingClassCourse.EducatorId))
                    .FirstOrDefaultAsync(w => w.WorkspaceId == selectedWorkspaceId);
                }

                if (workspace == null)
                {
                    return NotFound("Workspace not found");
                }

                return PartialView("_ConstraintsClass", workspace);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while loading constraints", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddConstraintForEducator([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            // Check for existing constraints at the same day and lesson
            var existingEducatorConstraint = await _context.EducatorConstraints
                .FirstOrDefaultAsync(ec => 
                    ec.WorkspaceId == selectedWorkspaceId &&
                    ec.EducatorId == constraint.EducatorId &&
                    ec.DayId == constraint.DayId &&
                    ec.LessonId == constraint.LessonId);

            var existingTimetableConstraint = await _context.TimetableConstraints
                .FirstOrDefaultAsync(tc =>
                    tc.WorkspaceId == selectedWorkspaceId &&
                    tc.DayId == constraint.DayId &&
                    tc.LessonId == constraint.LessonId);

            // If any constraint already exists at this slot, return an error
            if (existingEducatorConstraint != null || existingTimetableConstraint != null)
            {
                return BadRequest(new { message = $"Timetable constraint already exists at Day:{constraint.DayId}, Lesson:{constraint.LessonId}" });
            }

            if (constraint.ClassCourseId == 0)
            {
                EducatorConstraint educatorConstraint = new EducatorConstraint();
                educatorConstraint.WorkspaceId = selectedWorkspaceId;
                educatorConstraint.EducatorId = constraint.EducatorId;
                educatorConstraint.DayId = constraint.DayId;
                educatorConstraint.LessonId = constraint.LessonId;
                educatorConstraint.IsPlaceable = false;
                _context.EducatorConstraints.Add(educatorConstraint);
                await _context.SaveChangesAsync();
            }
            else
            {
                TimetableConstraint tc = new TimetableConstraint();
                tc.WorkspaceId = selectedWorkspaceId;
                tc.DayId = constraint.DayId;
                tc.LessonId = constraint.LessonId;
                tc.ClassCourseId = constraint.ClassCourseId;
                _context.TimetableConstraints.Add(tc);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveConstraintForEducator([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            EducatorConstraint educatorConstraint = _context.EducatorConstraints.FirstOrDefault(ec => ec.EducatorId == constraint.EducatorId && ec.DayId == constraint.DayId && ec.LessonId == constraint.LessonId);
            if (educatorConstraint != null)
            {
                _context.EducatorConstraints.Remove(educatorConstraint);
                await _context.SaveChangesAsync();
            }

            TimetableConstraint timetableConstraint = _context.TimetableConstraints.FirstOrDefault(tc => tc.DayId == constraint.DayId && tc.LessonId == constraint.LessonId);
            if (timetableConstraint != null)
            {
                _context.TimetableConstraints.Remove(timetableConstraint);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> SaveAllConstraints([FromBody] ConstraintChangesDTO changes)
        {
            int addedCount = 0;
            int removedCount = 0;
            List<string> errorMessages = new List<string>();
            
            try 
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));
                
                // ÖNEMLİ: İşlem sırasını değiştirdik - ÖNCE silme, SONRA ekleme
                // Bu şekilde, aynı hücrede bir constraint'i silip yerine başka bir constraint eklenebilir
                
                // 1. ADIM: Constraint silme işlemleri
                if (changes.ConstraintsToRemove != null && changes.ConstraintsToRemove.Any()) 
                {
                    foreach (var constraint in changes.ConstraintsToRemove)
                    {
                        try {
                            int localRemoveCount = 0;
                            
                            // Educator constraint kontrol et ve sil
                            if (constraint.EducatorId > 0)
                            {
                                var educatorConstraints = await _context.EducatorConstraints
                                    .Where(ec => 
                                        ec.EducatorId == constraint.EducatorId && 
                                        ec.DayId == constraint.DayId && 
                                        ec.LessonId == constraint.LessonId &&
                                        ec.WorkspaceId == selectedWorkspaceId)
                                    .ToListAsync();
                                
                                if (educatorConstraints.Any())
                                {
                                    foreach (var ec in educatorConstraints)
                                    {
                                        _context.EducatorConstraints.Remove(ec);
                                    }
                                    
                                    var result = await _context.SaveChangesAsync();
                                    localRemoveCount += result;
                                    
                                    if (result > 0)
                                    {
                                        Console.WriteLine($"Removed {result} EducatorConstraints at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    }
                                }
                            }

                            // Class constraint kontrol et ve sil
                            if (constraint.ClassId > 0)
                            {
                                var classConstraints = await _context.ClassConstraints
                                    .Where(cc => 
                                        cc.ClassId == constraint.ClassId && 
                                        cc.DayId == constraint.DayId && 
                                        cc.LessonId == constraint.LessonId &&
                                        cc.WorkspaceId == selectedWorkspaceId)
                                    .ToListAsync();
                                
                                if (classConstraints.Any())
                                {
                                    foreach (var cc in classConstraints)
                                    {
                                        _context.ClassConstraints.Remove(cc);
                                    }
                                    
                                    var result = await _context.SaveChangesAsync();
                                    localRemoveCount += result;
                                    
                                    if (result > 0)
                                    {
                                        Console.WriteLine($"Removed {result} ClassConstraints at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    }
                                }
                            }

                            // Classroom constraint kontrol et ve sil
                            if (constraint.ClassroomId > 0)
                            {
                                var classroomConstraints = await _context.ClassroomConstraints
                                    .Where(cr => 
                                        cr.ClassroomId == constraint.ClassroomId && 
                                        cr.DayId == constraint.DayId && 
                                        cr.LessonId == constraint.LessonId &&
                                        cr.WorkspaceId == selectedWorkspaceId)
                                    .ToListAsync();
                                
                                if (classroomConstraints.Any())
                                {
                                    foreach (var cr in classroomConstraints)
                                    {
                                        _context.ClassroomConstraints.Remove(cr);
                                    }
                                    
                                    var result = await _context.SaveChangesAsync();
                                    localRemoveCount += result;
                                    
                                    if (result > 0)
                                    {
                                        Console.WriteLine($"Removed {result} ClassroomConstraints at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    }
                                }
                            }

                            // Timetable constraint kontrol et ve sil
                            if (constraint.ClassCourseId > 0)
                            {
                                // Belirli bir ClassCourseId için silme
                                var timetableConstraints = await _context.TimetableConstraints
                                    .Where(tc => 
                                        tc.ClassCourseId == constraint.ClassCourseId && 
                                        tc.DayId == constraint.DayId && 
                                        tc.LessonId == constraint.LessonId &&
                                        tc.WorkspaceId == selectedWorkspaceId)
                                    .ToListAsync();
                                    
                                if (timetableConstraints.Any())
                                {
                                    foreach (var tc in timetableConstraints)
                                    {
                                        _context.TimetableConstraints.Remove(tc);
                                    }
                                    
                                    var result = await _context.SaveChangesAsync();
                                    localRemoveCount += result;
                                    
                                    if (result > 0)
                                    {
                                        Console.WriteLine($"Removed {result} TimetableConstraints for ClassCourseId={constraint.ClassCourseId} at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    }
                                }
                            }
                            else 
                            {
                                // ClassCourseId yoksa, o hücredeki tüm timetable kısıtlamalarını sil
                                // Bu, diğer constraint türleri için gerekli olabilir
                                var timetableConstraints = await _context.TimetableConstraints
                                    .Where(tc => 
                                        tc.DayId == constraint.DayId && 
                                        tc.LessonId == constraint.LessonId &&
                                        tc.WorkspaceId == selectedWorkspaceId)
                                    .ToListAsync();
                                    
                                if (timetableConstraints.Any())
                                {
                                    foreach (var tc in timetableConstraints)
                                    {
                                        _context.TimetableConstraints.Remove(tc);
                                    }
                                    
                                    var result = await _context.SaveChangesAsync();
                                    localRemoveCount += result;
                                    
                                    if (result > 0)
                                    {
                                        Console.WriteLine($"Removed {result} TimetableConstraints at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    }
                                }
                            }
                            
                            removedCount += localRemoveCount;
                            
                            if (localRemoveCount == 0)
                            {
                                // Sessizce devam et - bu bir hata durumu olmayabilir, 
                                // çünkü önce silindi sonra eklendi senaryosunda constraint bulunmayabilir
                                Console.WriteLine($"No constraints found to remove at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                            }
                        } 
                        catch (DbUpdateConcurrencyException ex) {
                            errorMessages.Add($"Concurrency error: {ex.Message}");
                        }
                        catch (Exception ex) {
                            errorMessages.Add($"Error removing constraint: {ex.Message}");
                        }
                    }
                }
                
                // 2. ADIM: Constraint ekleme işlemleri - artık silme işlemlerinden sonra gerçekleşiyor
                if (changes.ConstraintsToAdd != null && changes.ConstraintsToAdd.Any())
                {
                    foreach (var constraint in changes.ConstraintsToAdd)
                    {
                        try {
                            if (constraint.ClassCourseId == 0)
                            {
                                // EducatorId belirlenmemiş mi diye kontrol et
                                if (constraint.EducatorId <= 0)
                                {
                                    errorMessages.Add("EducatorId must be specified for educator constraints.");
                                    continue;
                                }
                                
                                // Aynı constraint var mı diye kontrol et ve varsa atlama yap
                                var existingConstraint = await _context.EducatorConstraints
                                    .FirstOrDefaultAsync(ec => 
                                        ec.EducatorId == constraint.EducatorId && 
                                        ec.DayId == constraint.DayId && 
                                        ec.LessonId == constraint.LessonId &&
                                        ec.WorkspaceId == selectedWorkspaceId);
                                    
                                if (existingConstraint != null)
                                {
                                    // Bu constraint zaten var, atlayalım
                                    errorMessages.Add($"Educator constraint already exists at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    continue;
                                }
                                
                                // Eğitmen constraint'i ekleme
                                EducatorConstraint educatorConstraint = new EducatorConstraint
                                {
                                    WorkspaceId = selectedWorkspaceId,
                                    EducatorId = constraint.EducatorId,
                                    DayId = constraint.DayId,
                                    LessonId = constraint.LessonId,
                                    IsPlaceable = false
                                };
                                
                                _context.EducatorConstraints.Add(educatorConstraint);
                                var result = await _context.SaveChangesAsync();
                                
                                // Gerçekten eklendi mi kontrol et
                                if (result > 0) 
                                {
                                    addedCount++;
                                    Console.WriteLine($"Added EducatorConstraint: EducatorId={constraint.EducatorId}, Day={constraint.DayId}, Lesson={constraint.LessonId}");
                                }
                                else
                                {
                                    errorMessages.Add($"Failed to add educator constraint at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                }
                            }
                            else
                            {
                                // Aynı constraint var mı diye kontrol et ve varsa atlama yap
                                var existingConstraint = await _context.TimetableConstraints
                                    .FirstOrDefaultAsync(tc => 
                                        tc.ClassCourseId == constraint.ClassCourseId && 
                                        tc.DayId == constraint.DayId && 
                                        tc.LessonId == constraint.LessonId &&
                                        tc.WorkspaceId == selectedWorkspaceId);
                                    
                                if (existingConstraint != null)
                                {
                                    // Bu constraint zaten var, atlayalım
                                    errorMessages.Add($"Timetable constraint already exists at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                    continue;
                                }
                                
                                // ClassCourseId kontrol et
                                var classCourse = await _context.ClassCourses.FindAsync(constraint.ClassCourseId);
                                if (classCourse == null)
                                {
                                    errorMessages.Add($"Invalid ClassCourseId: {constraint.ClassCourseId}");
                                    continue;
                                }
                                
                                // Zaman çizelgesi constraint'i ekleme
                                TimetableConstraint tc = new TimetableConstraint
                                {
                                    WorkspaceId = selectedWorkspaceId,
                                    DayId = constraint.DayId,
                                    LessonId = constraint.LessonId,
                                    ClassCourseId = constraint.ClassCourseId
                                };
                                
                                _context.TimetableConstraints.Add(tc);
                                var result = await _context.SaveChangesAsync();
                                
                                // Gerçekten eklendi mi kontrol et
                                if (result > 0) 
                                {
                                    addedCount++;
                                    Console.WriteLine($"Added TimetableConstraint: ClassCourseId={constraint.ClassCourseId}, Day={constraint.DayId}, Lesson:{constraint.LessonId}");
                                }
                                else
                                {
                                    errorMessages.Add($"Failed to add timetable constraint at Day:{constraint.DayId}, Lesson:{constraint.LessonId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorMessages.Add($"Error adding constraint: {ex.Message}");
                        }
                    }
                }
                
                // Sonuç mesajı oluştur
                string resultMessage = $"Changes saved. Added: {addedCount}, Removed: {removedCount}";
                if (errorMessages.Count > 0)
                {
                    resultMessage += ", with some errors.";
                }
                
                return Json(new { 
                    success = true, 
                    message = resultMessage,
                    addedCount = addedCount,
                    removedCount = removedCount,
                    errors = errorMessages.Count > 0 ? errorMessages : null
                });
            }
            catch (Exception ex)
            {
                // Hata detaylarını dahil et
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " Inner exception: " + ex.InnerException.Message;
                }
                
                // Hata durumunda
                errorMessages.Add(errorMessage);
                
                return Json(new { 
                    success = false, 
                    message = $"Error while saving changes: {errorMessage}",
                    errors = errorMessages
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ClearAllEducatorConstraints([FromBody] int educatorId)
        {
            try
            {
                Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

                // Delete all educator constraints
                var educatorConstraints = await _context.EducatorConstraints
                    .Where(ec => ec.EducatorId == educatorId && ec.WorkspaceId == selectedWorkspaceId)
                    .ToListAsync();
                _context.EducatorConstraints.RemoveRange(educatorConstraints);
                await _context.SaveChangesAsync();

                // Delete all timetable constraints for this educator's courses
                var classCourses = await _context.ClassCourses
                    .Where(cc => cc.EducatorId == educatorId)
                    .Select(cc => cc.ClassCourseId)
                    .ToListAsync();

                var timetableConstraints = await _context.TimetableConstraints
                    .Where(tc => classCourses.Contains(tc.ClassCourseId) && tc.WorkspaceId == selectedWorkspaceId)
                    .ToListAsync();
                _context.TimetableConstraints.RemoveRange(timetableConstraints);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All constraints cleared successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Failed to clear constraints: " + ex.Message });
            }
        }

        
        [HttpPost]
        public async Task<IActionResult> AddConstraintForClass([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            // Check for existing constraints at the same day and lesson
            var existingClassConstraint = await _context.ClassConstraints
                .FirstOrDefaultAsync(cc => 
                    cc.WorkspaceId == selectedWorkspaceId &&
                    cc.ClassId == constraint.ClassId &&
                    cc.DayId == constraint.DayId &&
                    cc.LessonId == constraint.LessonId);

            var existingTimetableConstraint = await _context.TimetableConstraints
                .FirstOrDefaultAsync(tc =>
                    tc.WorkspaceId == selectedWorkspaceId &&
                    tc.DayId == constraint.DayId &&
                    tc.LessonId == constraint.LessonId);

            // If any constraint already exists at this slot, return an error
            if (existingClassConstraint != null || existingTimetableConstraint != null)
            {
                return BadRequest(new { message = $"Timetable constraint already exists at Day:{constraint.DayId}, Lesson:{constraint.LessonId}" });
            }

            if (constraint.ClassCourseId == 0)
            {
                ClassConstraint classConstraint = new ClassConstraint();
                classConstraint.WorkspaceId = selectedWorkspaceId;
                classConstraint.ClassId = constraint.ClassId;
                classConstraint.DayId = constraint.DayId;
                classConstraint.LessonId = constraint.LessonId;
                classConstraint.IsPlaceable = false;
                _context.ClassConstraints.Add(classConstraint);
                await _context.SaveChangesAsync();
            }
            else
            {
                TimetableConstraint tc = new TimetableConstraint();
                tc.WorkspaceId = selectedWorkspaceId;
                tc.DayId = constraint.DayId;
                tc.LessonId = constraint.LessonId;
                tc.ClassCourseId = constraint.ClassCourseId;
                _context.TimetableConstraints.Add(tc);
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveConstraintForClass([FromBody] ConstraintDTO constraint)
        {
            Guid selectedWorkspaceId = Guid.Parse(User.FindFirstValue("WorkspaceId"));

            ClassConstraint classConstraint = await _context.ClassConstraints
                .FirstOrDefaultAsync(cc => 
                    cc.WorkspaceId == selectedWorkspaceId &&
                    cc.ClassId == constraint.ClassId && 
                    cc.DayId == constraint.DayId && 
                    cc.LessonId == constraint.LessonId);

            if (classConstraint != null)
            {
                _context.ClassConstraints.Remove(classConstraint);
                await _context.SaveChangesAsync();
            }

            TimetableConstraint timetableConstraint = await _context.TimetableConstraints
                .FirstOrDefaultAsync(tc => 
                    tc.WorkspaceId == selectedWorkspaceId &&
                    tc.DayId == constraint.DayId && 
                    tc.LessonId == constraint.LessonId);

            if (timetableConstraint != null)
            {
                _context.TimetableConstraints.Remove(timetableConstraint);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }
    }
}
