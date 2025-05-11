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
            
            // STEP 1: First, apply all timetable constraints (fixed placements)
            ApplyTimetableConstraints(data, placements);
            
            // STEP 2: Create a dictionary to track already scheduled hours per class course from fixed placements
            var scheduledHoursPerCourse = placements
                .GroupBy(p => p.ClassCourseId)
                .ToDictionary(g => g.Key, g => g.Count());
            
            // Create a list of class courses that need to be scheduled
            var coursesToSchedule = new List<ClassCourseScheduling>();
            
            foreach (var classCourse in data.ClassCourses)
            {
                // Skip if class course is for special purposes like "CLOSED" slots
                if (classCourse.ClassCourseId == 0)
                    continue;
                
                // Get the course weekly hour count and placement format
                int weeklyHours = classCourse.Course.WeeklyHourCount;
                
                // Önceden yerleştirilmiş blokları bul
                var existingPlacements = data.TimetableConstraints
                    .Where(tc => tc.ClassCourseId == classCourse.ClassCourseId)
                    .ToList();
                
                // Adjust for hours already scheduled via timetable constraints
                int alreadyScheduledHours = scheduledHoursPerCourse.ContainsKey(classCourse.ClassCourseId) ?
                    scheduledHoursPerCourse[classCourse.ClassCourseId] : 0;
                
                // Calculate remaining hours to schedule
                int remainingHours = weeklyHours - alreadyScheduledHours;
                
                // Skip if all hours already scheduled via timetable constraints
                if (remainingHours <= 0)
                    continue;
                    
                string placementFormat = classCourse.Course.PlacementFormat;
                
                // Orijinal formatı analiz et ve blok boyutlarını al
                var originalBlocks = new List<int>();
                if (!string.IsNullOrEmpty(placementFormat))
                {
                    var parts = placementFormat.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part.Trim(), out int blockSize) && blockSize > 0)
                        {
                            originalBlocks.Add(blockSize);
                        }
                    }
                }
                
                // Orijinal format geçerli mi kontrol et
                if (originalBlocks.Sum() != weeklyHours)
                {
                    // Format sorunlu, basitçe 1-saatlik bloklar kullan
                    originalBlocks.Clear();
                    for (int i = 0; i < weeklyHours; i++)
                    {
                        originalBlocks.Add(1);
                    }
                }
                
                // Önemli: TimetableConstraint'lere göre hangi bloklar tamamlandı bulalım
                var scheduledBlocks = IdentifyScheduledBlocks(classCourse.ClassCourseId, originalBlocks, existingPlacements, data);
                
                // Kalan (yerleştirilmemiş) blokları al
                var remainingBlocks = originalBlocks.Except(scheduledBlocks).ToList();
                
                // Kalan saatler (remainingHours) ile blok toplamları uyumlu mu? Kontrol edelim.
                if (remainingBlocks.Sum() != remainingHours)
                {
                    // Sorun var, daha güvenli yaklaşım uygula - kalan saatleri formatla uyumlu olarak düzenle
                    // Basitçe en büyük blokları öncelikle yerleştirmeye çalışalım
                    remainingBlocks.Clear();
                    var sortedBlocks = originalBlocks.OrderByDescending(b => b).ToList();
                    int hoursLeft = remainingHours;
                    
                    foreach (var block in sortedBlocks)
                    {
                        if (hoursLeft >= block)
                        {
                            remainingBlocks.Add(block);
                            hoursLeft -= block;
                        }
                        
                        if (hoursLeft == 0)
                            break;
                    }
                    
                    // Hala kalan saat varsa, 1-saatlik bloklarla tamamla
                    while (hoursLeft > 0)
                    {
                        remainingBlocks.Add(1);
                        hoursLeft--;
                    }
                }
                
                // Yerleştirilecek ders bloklarını ayarla (remainingBlocks tüm algoritmaya gidecek listedir)
                var lessonBlocks = remainingBlocks;
                
                // Add to courses to schedule
                coursesToSchedule.Add(new ClassCourseScheduling
                {
                    ClassCourse = classCourse,
                    LessonBlocks = lessonBlocks,
                    OriginalFormat = placementFormat  // Orijinal formatı sakla
                });
            }
            
            // Randomize order
            coursesToSchedule = coursesToSchedule.OrderBy(x => _random.Next()).ToList();
            
            // Her ders için, tüm blokları orijinal formatta yerleştirmeye çalış
            foreach (var courseToSchedule in coursesToSchedule)
            {
                // Tüm ders blokları için yerleştirme denenecek
                bool allBlocksPlaced = false;
                int globalAttempts = 0;
                
                while (!allBlocksPlaced && globalAttempts < 200) // Daha fazla deneme şansı
                {
                    globalAttempts++;
                    
                    // Her deneme için tüm blokların yerleştirilebileceği geçici bir liste oluştur
                    var temporaryPlacements = new List<PlacementRepresentation>();
                    bool allValid = true;
                    
                    // Get valid days (with LessonCount > 0)
                    var validDays = data.Days.Where(d => d.LessonCount > 0).ToList();
                    
                    // If no valid days, skip this attempt
                    if (!validDays.Any())
                        continue;
                    
                    // Orjinal ders bloklarını formatına uygun şekilde yerleştirmeye çalış
                    foreach (var lessonBlock in courseToSchedule.LessonBlocks)
                    {
                        // Her blok için yeni bir rastgele gün seç
                        int dayIndex = _random.Next(validDays.Count);
                        var day = validDays[dayIndex];
                        
                        // Gün için yeterli ders saati var mı?
                        if (day.LessonCount < lessonBlock)
                        {
                            allValid = false;
                            break;
                        }
                        
                        // O gün içindeki mümkün başlangıç ders saatleri
                        var possibleStartLessons = new List<int>();
                        
                        // Günün SADECE geçerli başlangıç ders saatlerini bul
                        // lessonBlock-1 kadar boş slot sonrası gerekli
                        var validStartLessons = data.Lessons
                            .Where(l => l.LessonNumber + lessonBlock - 1 <= day.LessonCount) // Yeterli ardışık slot var mı?
                            .Select(l => l.LessonId);
                            
                        possibleStartLessons.AddRange(validStartLessons);
                        
                        // Rastgele sırayla mümkün başlangıç saatlerini dene
                        possibleStartLessons = possibleStartLessons.OrderBy(x => _random.Next()).ToList();
                        
                        bool blockPlaced = false;
                        foreach (var startLessonId in possibleStartLessons)
                        {
                            // Bu blok için yerleştirme geçerli mi?
                            bool isValid = IsValidPlacement(
                                courseToSchedule.ClassCourse, 
                                day.DayId, 
                                startLessonId, 
                                lessonBlock, 
                                data, 
                                placements.Concat(temporaryPlacements).ToList());
                            
                            if (isValid)
                            {
                                // Blok içindeki her ders saati için ardışık yerleştirmeler yap
                                for (int i = 0; i < lessonBlock; i++)
                                {
                                    // Find the lesson with the right ID
                                    var currentLessonId = startLessonId + i;
                                    
                                    temporaryPlacements.Add(new PlacementRepresentation
                                    {
                                        ClassCourseId = courseToSchedule.ClassCourse.ClassCourseId,
                                        DayId = day.DayId,
                                        LessonId = currentLessonId
                                    });
                                }
                                
                                blockPlaced = true;
                                break; // Bu blok için geçerli bir yer bulundu, sonraki bloğa geç
                            }
                        }
                        
                        if (!blockPlaced)
                        {
                            // Bu blok için uygun bir yer bulunamadı, tüm bloklarla yeniden dene
                            allValid = false;
                            break;
                        }
                    }
                    
                    if (allValid)
                    {
                        // Tüm bloklar başarıyla yerleştirildi, geçici yerleştirmeleri kalıcı listeye ekle
                        placements.AddRange(temporaryPlacements);
                        allBlocksPlaced = true;
                    }
                }
                
                // Not: Eğer allBlocksPlaced = false ise, bu ders için hiçbir geçerli yerleştirme bulunamadı
                // Bu durum fitness fonksiyonunda olumsuz olarak değerlendirilecek
            }
            
            return placements;
        }

        /// <summary>
        /// Zaten yerleştirilmiş blokları analiz edip hangi boyuttaki blokların tamamlandığını belirler
        /// </summary>
        /// <param name="classCourseId">Sınıf ders ID</param>
        /// <param name="originalBlocks">Orijinal format blokları (3,2 gibi)</param>
        /// <param name="existingPlacements">Mevcut TimetableConstraint yerleştirilmeleri</param>
        /// <param name="data">Veri kaynağı</param>
        /// <returns>Yerleştirilmiş blok boyutları listesi</returns>
        private List<int> IdentifyScheduledBlocks(int classCourseId, List<int> originalBlocks, List<TimetableConstraint> existingPlacements, SchedulingData data)
        {
            var scheduledBlocks = new List<int>();
            if (!existingPlacements.Any())
                return scheduledBlocks; // Hiç yerleştirme yoksa boş liste döndür
                
            // Önce her gün için peş peşe yerleştirilmiş dersleri grupla
            var placementsByDay = existingPlacements
                .GroupBy(p => p.DayId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.LessonId).ToList());
                
            // Her gün için ardışık ders bloklarini belirle
            foreach (var dayGroup in placementsByDay)
            {
                var day = dayGroup.Key;
                var placements = dayGroup.Value;
                
                // Ardışık ders saatlerini belirle
                var consecutiveGroups = new List<List<TimetableConstraint>>();
                var currentGroup = new List<TimetableConstraint>();
                
                // Ardışık ders saatlerini gruplandır
                for (int i = 0; i < placements.Count; i++)
                {
                    if (i == 0 || placements[i].LessonId == placements[i-1].LessonId + 1)
                    {
                        // Ardışık ders, aynı gruba ekle
                        currentGroup.Add(placements[i]);
                    }
                    else
                    {
                        // Ardışık değil, yeni grup başlat
                        if (currentGroup.Any())
                        {
                            consecutiveGroups.Add(new List<TimetableConstraint>(currentGroup));
                            currentGroup.Clear();
                        }
                        currentGroup.Add(placements[i]);
                    }
                }
                
                // Son grubu ekle
                if (currentGroup.Any())
                {
                    consecutiveGroups.Add(currentGroup);
                }
                
                // Her grup için blok boyutu ekle
                foreach (var group in consecutiveGroups)
                {
                    var blockSize = group.Count;
                    
                    // Uygun bir blok boyutu bul
                    var bestMatch = FindBestBlockMatch(blockSize, originalBlocks.Except(scheduledBlocks).ToList());
                    if (bestMatch > 0)
                    {
                        scheduledBlocks.Add(bestMatch);
                    }
                }
            }
            
            return scheduledBlocks;
        }
        
        /// <summary>
        /// Bulunan blok boyutu için en uygun eşleşmeyi bulur
        /// </summary>
        private int FindBestBlockMatch(int blockSize, List<int> availableBlocks)
        {
            if (!availableBlocks.Any())
                return 0;
                
            // Tam eşleşme var mı?
            if (availableBlocks.Contains(blockSize))
                return blockSize;
                
            // Yoksa en yakın küçük blok
            var smallerBlocks = availableBlocks.Where(b => b < blockSize).OrderByDescending(b => b).ToList();
            if (smallerBlocks.Any())
                return smallerBlocks.First();
                
            // Yoksa en küçük büyük blok
            var largerBlocks = availableBlocks.Where(b => b > blockSize).OrderBy(b => b).ToList();
            if (largerBlocks.Any())
                return largerBlocks.First();
                
            return 0; // Eşleşme bulunamadı
        }
        
        private void ApplyTimetableConstraints(SchedulingData data, List<PlacementRepresentation> placements)
        {
            // Timetable constraints işleme stratejisi:
            // 1. Önce özel yerleştirmeleri uygula (ClassCourseId > 0)
            // 2. ClassCourseId=0 (CLOSED) olan kısıtlamaları kaydet, algoritma bunları dikkate alacak
            
            // 1. Özel yerleştirmeler (specified placements)
            var timetableAssignments = data.TimetableConstraints
                .Where(tc => tc.ClassCourseId > 0)
                .ToList();
            
            foreach (var assignment in timetableAssignments)
            {
                // Add this fixed placement to our solution
                placements.Add(new PlacementRepresentation
                {
                    ClassCourseId = assignment.ClassCourseId,
                    DayId = assignment.DayId,
                    LessonId = assignment.LessonId
                });
                
                // Track this placement to ensure we don't exceed required hours for this class course
                var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == assignment.ClassCourseId);
                if (classCourse != null)
                {
                    // Make sure we account for this hour in scheduling requirements
                    // This will be handled in the main scheduling algorithm
                }
            }
            
            // CLOSED (ClassCourseId=0) olan constraint'ler otomatik olarak IsValidPlacement metodunda kontrol edilecek
            // İşlem yapmıyoruz, çünkü bunlar placements listesine eklenmeyecek, sadece belirli saatler dolu olarak işaretlenecek
            // IsValidPlacement metodu zaten bunları kontrol ediyor
        }
        
        private bool IsValidPlacement(ClassCourse classCourse, int dayId, int startLessonId, int blockSize, 
            SchedulingData data, List<PlacementRepresentation> currentPlacements)
        {
            // Check if the day has any lessons (LessonCount > 0)
            var day = data.Days.FirstOrDefault(d => d.DayId == dayId);
            if (day == null || day.LessonCount == 0)
                return false;
                
            // Blok boyutu için gündeki ders sayısının yeterli olup olmadığını kontrol et
            if (day.LessonCount < blockSize)
                return false;
                
            // Yerleştirilecek blok için gerekli tüm derslerin bulunduğundan emin ol
            var lessonsToCheck = new List<int>();
            for (int i = 0; i < blockSize; i++)
            {
                int lessonId = startLessonId + i;
                // Dersin var olup olmadığını kontrol et
                if (!data.Lessons.Any(l => l.LessonId == lessonId))
                    return false;
                    
                lessonsToCheck.Add(lessonId);
            }
                
            // Sabit yerleştirmeler: TimetableConstraints'deki başka bir ders için atanımış slotları kontrol et
            // Eğer bu slot, TimetableConstraints'de zaten başka bir class course için ayrılmışsa, bu slot kullanılamaz
            if (data.TimetableConstraints.Any(tc => 
                tc.DayId == dayId && 
                lessonsToCheck.Contains(tc.LessonId) && 
                tc.ClassCourseId > 0 && 
                tc.ClassCourseId != classCourse.ClassCourseId))
                return false;

            // Get relevant constraint IDs
            var classId = classCourse.ClassId;
            var educatorId = classCourse.EducatorId;
            var classroomId = classCourse.ClassroomId;
            var courseId = classCourse.CourseId;
            
            // Tüm blok için bir kerede constraint kontrollerini yap
            // Tüm constraint tipleri için, blokun tüm derslerini kontrol et
            
            // 1. Sınıf kısıtlaması kontrolü (ClassConstraints)
            if (data.ClassConstraints.Any(c => 
                c.ClassId == classId && 
                c.DayId == dayId && 
                lessonsToCheck.Contains(c.LessonId) && 
                !c.IsPlaceable))
            {
                return false; // Kapalı sınıf zamanı bulundu
            }
            
            // 2. Eğitimci kısıtlaması kontrolü (EducatorConstraints)
            if (data.EducatorConstraints.Any(c => 
                c.EducatorId == educatorId && 
                c.DayId == dayId && 
                lessonsToCheck.Contains(c.LessonId) && 
                !c.IsPlaceable))
            {
                return false; // Kapalı eğitimci zamanı bulundu
            }
            
            // 3. Derslik kısıtlaması kontrolü (ClassroomConstraints)
            if (data.ClassroomConstraints.Any(c => 
                c.ClassroomId == classroomId && 
                c.DayId == dayId && 
                lessonsToCheck.Contains(c.LessonId) && 
                !c.IsPlaceable))
            {
                return false; // Kapalı derslik zamanı bulundu
            }
            
            // 4. "CLOSED" TimetableConstraint kontrolü (ClassCourseId=0)
            if (data.TimetableConstraints.Any(tc => 
                tc.DayId == dayId && 
                lessonsToCheck.Contains(tc.LessonId) && 
                tc.ClassCourseId == 0))
            {
                return false; // Kapalı (CLOSED) slot bulundu
            }
            
            // 5. Mevcut yerleştirmelerdeki çakışmaları kontrol et
            // Aynı sınıf, eğitimci veya derslik için herhangi bir çakışma olup olmadığını kontrol et
            if (currentPlacements.Any(p => 
                p.DayId == dayId && 
                lessonsToCheck.Contains(p.LessonId) && 
                (
                    data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassId == classId ||
                    data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.EducatorId == educatorId ||
                    data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == p.ClassCourseId)?.ClassroomId == classroomId
                )))
            {
                return false; // Çakışan yerleştirme bulundu
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
                    DayId = day.DayId, // Eklenen DayId alanı
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
        public ClassCourse ClassCourse { get; set; }
        public List<int> LessonBlocks { get; set; }
        public string OriginalFormat { get; set; } // Orijinal format bilgisini sakla
    }
}
