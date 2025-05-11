using GWOTimetable.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GWOTimetable.Services
{
    public class GWOSchedulerService
    {
        private readonly Db12026Context _context;
        private Random _random;
        private const int MAX_ITERATIONS = 100;
        private const int POPULATION_SIZE = 20; // Number of wolves in the pack
        
        // GWO parameters
        private const double A_COEF_START = 2.0;
        private const double A_COEF_END = 0.0;

        public GWOSchedulerService(Db12026Context context)
        {
            _context = context;
            _random = new Random();
        }

        public async Task<bool> RunSchedulerAsync(int timetableId)
        {
            try
            {
                // Get the timetable for which we're creating a schedule
                var timetable = await _context.Timetables
                    .FirstOrDefaultAsync(t => t.TimetableId == timetableId);
                
                if (timetable == null)
                    return false;
                
                // Update timetable state to "Processing" (assuming stateId 2 is Processing)
                timetable.TimetableStateId = 2;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                
                // Load all constraints and required data
                var workspaceId = timetable.WorkspaceId;
                var data = await LoadRequiredDataAsync(workspaceId);
                
                // Run GWO algorithm
                var placements = RunGWOAlgorithm(data, timetableId);
                
                // Save results
                await SavePlacementsAsync(placements, timetable);
                
                // Update timetable state to "Completed" (assuming stateId 3 is Completed)
                timetable.TimetableStateId = 3;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scheduling error: {ex.Message}");
                
                // Update timetable state to "Failed" (assuming stateId 4 is Failed)
                var timetable = await _context.Timetables
                    .FirstOrDefaultAsync(t => t.TimetableId == timetableId);
                
                if (timetable != null)
                {
                    timetable.TimetableStateId = 4;
                    timetable.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                
                return false;
            }
        }

        private async Task<SchedulingData> LoadRequiredDataAsync(Guid workspaceId)
        {
            // Load all necessary data for scheduling from database
            var data = new SchedulingData
            {
                // Load all class constraints
                ClassConstraints = await _context.ClassConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),
                
                // Load all educator constraints
                EducatorConstraints = await _context.EducatorConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),
                
                // Load all classroom constraints
                ClassroomConstraints = await _context.ClassroomConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),
                
                // Load all timetable constraints
                TimetableConstraints = await _context.TimetableConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),
                
                // Load class courses (which classes have which courses)
                ClassCourses = await _context.ClassCourses
                    .Where(cc => cc.WorkspaceId == workspaceId)
                    .Include(cc => cc.Class)
                    .Include(cc => cc.Course)
                    .Include(cc => cc.Classroom)
                    .Include(cc => cc.Educator)
                    .ToListAsync(),
                
                // Load days
                Days = await _context.Days
                    .Where(d => d.WorkspaceId == workspaceId)
                    .ToListAsync(),
                
                // Load lessons (time slots)
                Lessons = await _context.Lessons
                    .Where(l => l.WorkspaceId == workspaceId)
                    .ToListAsync()
            };
            
            return data;
        }

        private List<TimetablePlacement> RunGWOAlgorithm(SchedulingData data, int timetableId)
        {
            // Initialize the wolf population with random solutions
            var wolves = InitializeWolfPopulation(data, POPULATION_SIZE);
            
            // Initialize Alpha, Beta, and Delta wolves
            var alphaWolf = new Wolf { Fitness = double.MaxValue };
            var betaWolf = new Wolf { Fitness = double.MaxValue };
            var deltaWolf = new Wolf { Fitness = double.MaxValue };
            
            // Main GWO algorithm loop
            for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
            {
                // Calculate the coefficient 'a' which decreases linearly from 2 to 0
                double a = A_COEF_START - iteration * ((A_COEF_START - A_COEF_END) / MAX_ITERATIONS);
                
                // For each wolf in the population
                foreach (var wolf in wolves)
                {
                    // Evaluate fitness of the wolf (solution quality)
                    wolf.Fitness = EvaluateFitness(wolf.Placements, data);
                    
                    // Update Alpha, Beta, and Delta wolves
                    if (wolf.Fitness < alphaWolf.Fitness)
                    {
                        deltaWolf = betaWolf;
                        betaWolf = alphaWolf;
                        alphaWolf = new Wolf
                        {
                            Placements = wolf.Placements.Select(p => p.Clone()).ToList(),
                            Fitness = wolf.Fitness
                        };
                    }
                    else if (wolf.Fitness < betaWolf.Fitness)
                    {
                        deltaWolf = betaWolf;
                        betaWolf = new Wolf
                        {
                            Placements = wolf.Placements.Select(p => p.Clone()).ToList(),
                            Fitness = wolf.Fitness
                        };
                    }
                    else if (wolf.Fitness < deltaWolf.Fitness)
                    {
                        deltaWolf = new Wolf
                        {
                            Placements = wolf.Placements.Select(p => p.Clone()).ToList(),
                            Fitness = wolf.Fitness
                        };
                    }
                }
                
                // Update each wolf's position
                foreach (var wolf in wolves)
                {
                    // Only update if not alpha, beta, or delta
                    if (wolf.Fitness != alphaWolf.Fitness && 
                        wolf.Fitness != betaWolf.Fitness && 
                        wolf.Fitness != deltaWolf.Fitness)
                    {
                        UpdateWolfPosition(wolf, alphaWolf, betaWolf, deltaWolf, a, data);
                    }
                }
                
                // Optional: Check if we've reached an acceptable solution (e.g., no constraint violations)
                if (alphaWolf.Fitness == 0)
                    break;
            }
            
            // Return the best solution found (Alpha wolf's placements)
            return ConvertToTimetablePlacements(alphaWolf.Placements, data, timetableId);
        }

        private List<Wolf> InitializeWolfPopulation(SchedulingData data, int populationSize)
        {
            var population = new List<Wolf>();
            
            for (int i = 0; i < populationSize; i++)
            {
                var wolf = new Wolf
                {
                    Placements = GenerateRandomSolution(data),
                    Fitness = 0.0
                };
                
                population.Add(wolf);
            }
            
            return population;
        }

        private List<PlacementRepresentation> GenerateRandomSolution(SchedulingData data)
        {
            var placements = new List<PlacementRepresentation>();
            
            // Create a list of class courses that need to be scheduled
            var coursesToSchedule = new List<ClassCourseScheduling>();
            
            foreach (var classCourse in data.ClassCourses)
            {
                // Skip if class course is for special purposes like "CLOSED" slots
                if (classCourse.ClassCourseId == 0)
                    continue;
                
                // Get the course weekly hour count and placement format
                int weeklyHours = classCourse.Course.WeeklyHourCount;
                string placementFormat = classCourse.Course.PlacementFormat;
                
                // Analyze placement format
                var lessonBlocks = new List<int>();
                if (!string.IsNullOrEmpty(placementFormat))
                {
                    var parts = placementFormat.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part.Trim(), out int blockSize))
                        {
                            lessonBlocks.Add(blockSize);
                        }
                    }
                }
                
                // If no valid format or total does not match weekly hours, default to 1-hour blocks
                if (lessonBlocks.Sum() != weeklyHours)
                {
                    lessonBlocks.Clear();
                    for (int i = 0; i < weeklyHours; i++)
                    {
                        lessonBlocks.Add(1);
                    }
                }
                
                // Add to courses to schedule
                coursesToSchedule.Add(new ClassCourseScheduling
                {
                    ClassCourse = classCourse,
                    LessonBlocks = lessonBlocks
                });
            }
            
            // Randomize order
            coursesToSchedule = coursesToSchedule.OrderBy(x => _random.Next()).ToList();
            
            // For each class course, try to find valid placements
            foreach (var courseToSchedule in coursesToSchedule)
            {
                foreach (var lessonBlock in courseToSchedule.LessonBlocks)
                {
                    // Try to place this block of lessons
                    bool placed = false;
                    int attempts = 0;
                    
                    while (!placed && attempts < 100)
                    {
                        attempts++;
                        
                        // Get valid days (with LessonCount > 0)
                        var validDays = data.Days.Where(d => d.LessonCount > 0).ToList();
                        
                        // If no valid days, continue to next attempt
                        if (!validDays.Any())
                            continue;
                            
                        // Random day and starting lesson
                        int dayIndex = _random.Next(validDays.Count);
                        int lessonIndex = _random.Next(data.Lessons.Count - lessonBlock + 1);
                        
                        var day = validDays[dayIndex];
                        var startLesson = data.Lessons[lessonIndex];
                        
                        // Check if this placement is valid
                        bool isValid = IsValidPlacement(courseToSchedule.ClassCourse, day.DayId, startLesson.LessonId, lessonBlock, data, placements);
                        
                        if (isValid)
                        {
                            // Add placements for each hour in the block
                            for (int i = 0; i < lessonBlock; i++)
                            {
                                var currentLesson = data.Lessons[lessonIndex + i];
                                
                                placements.Add(new PlacementRepresentation
                                {
                                    ClassCourseId = courseToSchedule.ClassCourse.ClassCourseId,
                                    DayId = day.DayId,
                                    LessonId = currentLesson.LessonId
                                });
                            }
                            
                            placed = true;
                        }
                    }
                    
                    // If we couldn't place after max attempts, just continue
                    // This will affect the fitness score negatively
                }
            }
            
            return placements;
        }

        private bool IsValidPlacement(ClassCourse classCourse, int dayId, int startLessonId, int blockSize, 
            SchedulingData data, List<PlacementRepresentation> currentPlacements)
        {
            // Check if the day has any lessons (LessonCount > 0)
            var day = data.Days.FirstOrDefault(d => d.DayId == dayId);
            if (day == null || day.LessonCount == 0)
                return false;

            // Get relevant constraints
            var classId = classCourse.ClassId;
            var educatorId = classCourse.EducatorId;
            var classroomId = classCourse.ClassroomId;
            
            // For each lesson in the block
            for (int i = 0; i < blockSize; i++)
            {
                int currentLessonId = startLessonId + i;
                
                // Make sure the lesson ID is valid
                if (!data.Lessons.Any(l => l.LessonId == currentLessonId))
                    return false;
                
                // Check existing placements for conflicts
                if (currentPlacements.Any(p => 
                    (p.DayId == dayId && p.LessonId == currentLessonId) && 
                    (
                        data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassId == classId ||
                        data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.EducatorId == educatorId ||
                        data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassroomId == classroomId
                    )))
                {
                    return false;
                }
                
                // Check class constraints
                if (data.ClassConstraints.Any(c => 
                    c.ClassId == classId && 
                    c.DayId == dayId && 
                    c.LessonId == currentLessonId && 
                    !c.IsPlaceable))
                {
                    return false;
                }
                
                // Check educator constraints
                if (data.EducatorConstraints.Any(c => 
                    c.EducatorId == educatorId && 
                    c.DayId == dayId && 
                    c.LessonId == currentLessonId && 
                    !c.IsPlaceable))
                {
                    return false;
                }
                
                // Check classroom constraints
                if (data.ClassroomConstraints.Any(c => 
                    c.ClassroomId == classroomId && 
                    c.DayId == dayId && 
                    c.LessonId == currentLessonId && 
                    !c.IsPlaceable))
                {
                    return false;
                }
            }
            
            return true;
        }

        private double EvaluateFitness(List<PlacementRepresentation> placements, SchedulingData data)
        {
            double violations = 0;
            
            // Check for hard constraint violations
            
            // 1. Check for double scheduling of classes
            var classViolations = placements
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassId).Distinct().Count() > 1)
                .Count();
            
            violations += classViolations * 1000;
            
            // 2. Check for double scheduling of educators
            var educatorViolations = placements
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.EducatorId).Distinct().Count() > 1)
                .Count();
            
            violations += educatorViolations * 1000;
            
            // 3. Check for double scheduling of classrooms
            var classroomViolations = placements
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassroomId).Distinct().Count() > 1)
                .Count();
            
            violations += classroomViolations * 1000;
            
            // 4. Check for missing lessons (not all courses fully scheduled)
            var scheduledHours = placements
                .GroupBy(p => p.ClassCourseId)
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var classCourse in data.ClassCourses)
            {
                // Skip special ClassCourseId=0 used for "CLOSED" constraints
                if (classCourse.ClassCourseId == 0)
                    continue;
                
                int requiredHours = classCourse.Course.WeeklyHourCount;
                int scheduledHoursCount = scheduledHours.ContainsKey(classCourse.ClassCourseId) ? scheduledHours[classCourse.ClassCourseId] : 0;
                
                if (scheduledHoursCount < requiredHours)
                {
                    violations += (requiredHours - scheduledHoursCount) * 500;
                }
            }
            
            // Check for constraint violations
            foreach (var placement in placements)
            {
                var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == placement.ClassCourseId);
                if (classCourse != null)
                {
                    // Check class constraints
                    if (data.ClassConstraints.Any(c => 
                        c.ClassId == classCourse.ClassId && 
                        c.DayId == placement.DayId && 
                        c.LessonId == placement.LessonId && 
                        !c.IsPlaceable))
                    {
                        violations += 2000;
                    }
                    
                    // Check educator constraints
                    if (data.EducatorConstraints.Any(c => 
                        c.EducatorId == classCourse.EducatorId && 
                        c.DayId == placement.DayId && 
                        c.LessonId == placement.LessonId && 
                        !c.IsPlaceable))
                    {
                        violations += 2000;
                    }
                    
                    // Check classroom constraints
                    if (data.ClassroomConstraints.Any(c => 
                        c.ClassroomId == classCourse.ClassroomId && 
                        c.DayId == placement.DayId && 
                        c.LessonId == placement.LessonId && 
                        !c.IsPlaceable))
                    {
                        violations += 2000;
                    }
                }
            }
            
            // Add soft constraints penalties (can be adjusted based on preference)
            // For example, avoid scheduling the same class for too many consecutive hours
            
            return violations;
        }

        private void UpdateWolfPosition(Wolf wolf, Wolf alpha, Wolf beta, Wolf delta, double a, SchedulingData data)
        {
            // Create a set of new placements by modifying the wolf's current solution
            var newPlacements = new List<PlacementRepresentation>();
            
            // For each placement in the wolf's solution
            foreach (var placement in wolf.Placements)
            {
                // Calculate A and C coefficients
                double r1 = _random.NextDouble();
                double r2 = _random.NextDouble();
                double A = 2 * a * r1 - a; // A decreases linearly from 2 to 0
                double C = 2 * r2;
                
                // Apply GWO position update formula: X(t+1) = (X_alpha + X_beta + X_delta) / 3
                // However, since our solution is a discrete set of placements, we need to adapt this
                
                if (Math.Abs(A) < 1.0) // Exploitation
                {
                    // In exploitation, move towards the best wolves by copying a random selection of their placements
                    double rand = _random.NextDouble();
                    
                    if (rand < 0.6) // 60% chance to follow alpha wolf
                    {
                        // Try to use alpha's placement for this slot
                        var alphaPlacement = alpha.Placements.FirstOrDefault(p => 
                            p.DayId == placement.DayId && p.LessonId == placement.LessonId);
                        
                        if (alphaPlacement != null)
                        {
                            newPlacements.Add(alphaPlacement.Clone());
                        }
                        else
                        {
                            // Alpha doesn't have a placement for this slot, keep the current placement
                            newPlacements.Add(placement.Clone());
                        }
                    }
                    else if (rand < 0.9) // 30% chance to follow beta wolf
                    {
                        var betaPlacement = beta.Placements.FirstOrDefault(p => 
                            p.DayId == placement.DayId && p.LessonId == placement.LessonId);
                        
                        if (betaPlacement != null)
                        {
                            newPlacements.Add(betaPlacement.Clone());
                        }
                        else
                        {
                            newPlacements.Add(placement.Clone());
                        }
                    }
                    else // 10% chance to follow delta wolf
                    {
                        var deltaPlacement = delta.Placements.FirstOrDefault(p => 
                            p.DayId == placement.DayId && p.LessonId == placement.LessonId);
                        
                        if (deltaPlacement != null)
                        {
                            newPlacements.Add(deltaPlacement.Clone());
                        }
                        else
                        {
                            newPlacements.Add(placement.Clone());
                        }
                    }
                }
                else // Exploration
                {
                    // In exploration, try to find new solutions by moving placements randomly
                    if (_random.NextDouble() < 0.3) // 30% chance to move a placement
                    {
                        // Get the class course that's being placed
                        var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == placement.ClassCourseId);
                        
                        if (classCourse != null)
                        {
                            // Try to find a new valid position
                            bool placed = false;
                            int attempts = 0;
                            
                            while (!placed && attempts < 10)
                            {
                                attempts++;
                                
                                // Random day and lesson
                                int dayIndex = _random.Next(data.Days.Count);
                                int lessonIndex = _random.Next(data.Lessons.Count);
                                
                                var day = data.Days[dayIndex];
                                var lesson = data.Lessons[lessonIndex];
                                
                                // Check if this placement is valid
                                bool isValid = IsValidPlacement(classCourse, day.DayId, lesson.LessonId, 1, data, 
                                    wolf.Placements.Where(p => p != placement).ToList());
                                
                                if (isValid)
                                {
                                    newPlacements.Add(new PlacementRepresentation
                                    {
                                        ClassCourseId = classCourse.ClassCourseId,
                                        DayId = day.DayId,
                                        LessonId = lesson.LessonId
                                    });
                                    
                                    placed = true;
                                }
                            }
                            
                            if (!placed)
                            {
                                // Couldn't find a valid new position, keep the original
                                newPlacements.Add(placement.Clone());
                            }
                        }
                        else
                        {
                            // Couldn't find class course, keep the original
                            newPlacements.Add(placement.Clone());
                        }
                    }
                    else
                    {
                        // Keep the original placement
                        newPlacements.Add(placement.Clone());
                    }
                }
            }
            
            // Update the wolf's placements with the new ones
            wolf.Placements = newPlacements;
        }

        private List<TimetablePlacement> ConvertToTimetablePlacements(List<PlacementRepresentation> placements, 
            SchedulingData data, int timetableId)
        {
            var result = new List<TimetablePlacement>();
            
            foreach (var placement in placements)
            {
                var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == placement.ClassCourseId);
                if (classCourse == null)
                    continue;
                
                var day = data.Days.FirstOrDefault(d => d.DayId == placement.DayId);
                if (day == null)
                    continue;
                
                var lesson = data.Lessons.FirstOrDefault(l => l.LessonId == placement.LessonId);
                if (lesson == null)
                    continue;
                
                result.Add(new TimetablePlacement
                {
                    WorkspaceId = classCourse.WorkspaceId,
                    DayOfWeek = day.DayOfWeek,
                    DayShortName = day.ShortName,
                    LessonNumber = lesson.LessonNumber,
                    StartTime = lesson.StartTime,
                    EndTime = lesson.EndTime,
                    CourseCode = classCourse.Course.CourseCode,
                    CourseName = classCourse.Course.CourseName,
                    ClassroomName = classCourse.Classroom.ClassroomName,
                    ClassName = classCourse.Class.ClassName,
                    EducatorFullName = $"{classCourse.Educator.Title} {classCourse.Educator.FirstName} {classCourse.Educator.LastName}".Trim(),
                    EducatorShortName = classCourse.Educator.ShortName
                });
            }
            
            return result;
        }

        private async Task SavePlacementsAsync(List<TimetablePlacement> placements, Timetable timetable)
        {
            // First delete any existing placements for this timetable ID
            var existingPlacements = await _context.TimetablePlacements
                .Where(tp => tp.WorkspaceId == timetable.WorkspaceId)
                .ToListAsync();
                
            _context.TimetablePlacements.RemoveRange(existingPlacements);
            
            // Add the new placements
            await _context.TimetablePlacements.AddRangeAsync(placements);
            await _context.SaveChangesAsync();
        }
    }

    // Helper classes for GWO algorithm
    public class SchedulingData
    {
        public List<ClassConstraint> ClassConstraints { get; set; } = new List<ClassConstraint>();
        public List<EducatorConstraint> EducatorConstraints { get; set; } = new List<EducatorConstraint>();
        public List<ClassroomConstraint> ClassroomConstraints { get; set; } = new List<ClassroomConstraint>();
        public List<TimetableConstraint> TimetableConstraints { get; set; } = new List<TimetableConstraint>();
        public List<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();
        public List<Day> Days { get; set; } = new List<Day>();
        public List<Lesson> Lessons { get; set; } = new List<Lesson>();
    }

    public class Wolf
    {
        public List<PlacementRepresentation> Placements { get; set; } = new List<PlacementRepresentation>();
        public double Fitness { get; set; }
    }

    public class PlacementRepresentation
    {
        public int ClassCourseId { get; set; }
        public int DayId { get; set; }
        public int LessonId { get; set; }
        
        public PlacementRepresentation Clone()
        {
            return new PlacementRepresentation
            {
                ClassCourseId = this.ClassCourseId,
                DayId = this.DayId,
                LessonId = this.LessonId
            };
        }
    }

    public class ClassCourseScheduling
    {
        public ClassCourse? ClassCourse { get; set; }
        public List<int> LessonBlocks { get; set; } = new List<int>();
    }
}
