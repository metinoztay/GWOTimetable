using GWOTimetable.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GWOTimetable.Services
{
    public class AdvancedGWOSchedulerService
    {
        private readonly Db12026Context _context;
        private Random _random;
        private const int MAX_ITERATIONS = 500;
        private const int POPULATION_SIZE = 3;
        
        // GWO parametreleri
        private const double A_COEF_START = 2.0;
        private const double A_COEF_END = 0.0;

        public AdvancedGWOSchedulerService(Db12026Context context)
        {
            _context = context;
            _random = new Random();
        }

        public async Task<bool> RunSchedulerAsync(int timetableId)
        {
            try
            {
                // Zaman tablosunu getir, bunun için bir schedule oluşturuyoruz
                var timetable = await _context.Timetables
                    .FirstOrDefaultAsync(t => t.TimetableId == timetableId);

                if (timetable == null)
                    return false;

                // Zaman tablosu durumunu "Processing" (varsayılan olarak stateId 2) olarak güncelle
                timetable.TimetableStateId = 2;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                // Tüm kısıtlamaları ve gerekli verileri yükle
                var workspaceId = timetable.WorkspaceId;
                var data = await LoadRequiredDataAsync(workspaceId);

                // GWO algoritmasını çalıştır ve sonucu doğrudan al
                var result = RunGWOAlgorithm(data, timetableId);
                
                // Alpha kurdun yerleşimlerini doğrudan RunGWOAlgorithm'den al
                var alphaWolfPlacements = result.Item2; // RunGWOAlgorithm metodundan dönen alpha kurt yerleşimleri
                
                Console.WriteLine("Veritabanına kaydedilecek yerleşimler:");
                PrintPlacements(alphaWolfPlacements, data);
                
                // Sonuçları kaydet
                await SavePlacementsAsync(alphaWolfPlacements, timetable, data);

                // Zaman tablosu durumunu "Completed" (varsayılan olarak stateId 3) olarak güncelle
                timetable.TimetableStateId = 3;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scheduling hatası: {ex.Message}");

                // Zaman tablosu durumunu "Failed" (varsayılan olarak stateId 4) olarak güncelle
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
            // Veritabanından gerekli bütün verileri yükle
            var data = new SchedulingData
            {
                // Sınıf kısıtlamalarını yükle
                ClassConstraints = await _context.ClassConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),

                // Eğitimci kısıtlamalarını yükle
                EducatorConstraints = await _context.EducatorConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),

                // Derslik kısıtlamalarını yükle
                ClassroomConstraints = await _context.ClassroomConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),

                // Zaman tablosu kısıtlamalarını yükle
                TimetableConstraints = await _context.TimetableConstraints
                    .Where(c => c.WorkspaceId == workspaceId)
                    .ToListAsync(),

                // Sınıf derslerini yükle (hangi sınıflar hangi dersleri alıyor)
                ClassCourses = await _context.ClassCourses
                    .Where(cc => cc.WorkspaceId == workspaceId)
                    .Include(cc => cc.Class)
                    .Include(cc => cc.Course)
                    .Include(cc => cc.Classroom)
                    .Include(cc => cc.Educator)
                    .ToListAsync(),

                // Günleri yükle
                Days = await _context.Days
                    .Where(d => d.WorkspaceId == workspaceId)
                    .ToListAsync(),

                // Ders saatlerini yükle
                Lessons = await _context.Lessons
                    .Where(l => l.WorkspaceId == workspaceId)
                    .ToListAsync()
            };

            return data;
        }

        private Tuple<List<CourseBlock>, List<PlacementRepresentation>> RunGWOAlgorithm(SchedulingData data, int timetableId)
        {
            
            var lessonBlocks = CreateSortedLessonBlocks(data);
            
            // Yerleştirilecek toplam ders sayısı hesaplaması
            int blockLessonsCount = lessonBlocks.Sum(block => block.BlockSize);
            
            // TimetableConstraint ile önceden yerleştirilmiş dersleri say
            int preplacedLessonsCount = data.TimetableConstraints.Count;
            
            // Toplam yerleştirilmesi gereken ders sayısı
            int totalLessonsToPlace = blockLessonsCount + preplacedLessonsCount;

            // Kurt sürüsünü başlat
            var wolves = InitializeWolfPopulation(lessonBlocks, data, POPULATION_SIZE);
            
            // Her kurt için fitness hesapla
            foreach (var wolf in wolves)
            {
                wolf.Fitness = CalculateFitness(wolf.Placements, lessonBlocks, data);
            }
            
            // Alpha, Beta ve Delta kurtlarını başlat
            Wolf alphaWolf = null;
            Wolf betaWolf = null;
            Wolf deltaWolf = null;
            
            // En iyi 3 kurdu seç (fitness'a göre sıralayarak)
            UpdateLeaderWolves(wolves, ref alphaWolf, ref betaWolf, ref deltaWolf);
            
            // En iyi çözümü log
            Console.WriteLine($"Başlangıç - Alpha fitness: {alphaWolf.Fitness}, Yerleştirilen dersler: {alphaWolf.Placements.Count}/{totalLessonsToPlace}");
            
            // GWO iterasyonları
            int iteration = 0;
            bool allLessonsPlaced = false;
            
            while (iteration < MAX_ITERATIONS && !allLessonsPlaced)
            {
                // a parametresi güncelle (A_COEF_START'dan A_COEF_END'e doğru doğrusal azalır)
                double a = A_COEF_START - ((double)iteration / MAX_ITERATIONS) * (A_COEF_START - A_COEF_END);
                
                // Her kurt için pozisyonu güncelle
                foreach (var wolf in wolves)
                {
                    if (wolf == alphaWolf || wolf == betaWolf || wolf == deltaWolf)
                        continue; // Lider kurtları güncelleme
                    
                    // Yeni pozisyon hesapla (yerleşimler)
                    var newPlacements = CalculateNewPosition(wolf, alphaWolf, betaWolf, deltaWolf, a, lessonBlocks, data);
                    
                    // Yeni fitness hesapla
                    double newFitness = CalculateFitness(newPlacements, lessonBlocks, data);
                    
                    // Eğer yeni pozisyon daha iyiyse, kurdu güncelle
                    if (newFitness > wolf.Fitness)
                    {
                        wolf.Placements = newPlacements;
                        wolf.Fitness = newFitness;
                    }
                }
                
                // Lider kurtları güncelle
                UpdateLeaderWolves(wolves, ref alphaWolf, ref betaWolf, ref deltaWolf);
                
                // Durma koşullarını kontrol et
                // 1. Tüm dersler yerleştirildi mi?
                if (alphaWolf.Placements.Count == totalLessonsToPlace)
                {
                    // Tüm derslerin format kontrollerini yap
                    bool allFormatsCorrect = true;
                    foreach (var classCourse in data.ClassCourses.Where(cc => cc.ClassCourseId > 0))
                    {
                        int requiredHours = classCourse.Course.WeeklyHourCount;
                        if (requiredHours <= 0) continue; // Sıfır saatlik dersleri atla
                        
                        var currentCoursePlacements = alphaWolf.Placements
                            .Where(p => p.ClassCourseId == classCourse.ClassCourseId)
                            .ToList();
                        
                        // Ders saati kontrolü
                        if (currentCoursePlacements.Count != requiredHours)
                        {
                            allFormatsCorrect = false;
                            break;
                        }
                        
                        // Format kontrolü
                        if (!string.IsNullOrEmpty(classCourse.Course.PlacementFormat))
                        {
                            List<int> originalFormatBlocks = classCourse.Course.PlacementFormat
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => int.TryParse(p.Trim(), out int val) ? val : 0)
                                .Where(val => val > 0)
                                .ToList();
                            
                            if (originalFormatBlocks.Any() && originalFormatBlocks.Sum() == requiredHours)
                            {
                                var actualBlocks = GetActualPlacedBlocks(currentCoursePlacements, data);
                                originalFormatBlocks.Sort();
                                
                                if (!originalFormatBlocks.SequenceEqual(actualBlocks))
                                {
                                    allFormatsCorrect = false;
                                    break;
                                }
                            }
                        }
                    }
                    
                    // Tüm dersler yerleştirilmiş ve format doğru ise, döngüden çık
                    if (allFormatsCorrect)
                    {
                        allLessonsPlaced = true;
                        Console.WriteLine($"Tüm dersler doğru formatta {iteration}. iterasyonda yerleştirildi! İyileştirme gerekmeyecek.");
                    }
                    else
                    {
                        Console.WriteLine($"Tüm dersler {iteration}. iterasyonda yerleştirildi, ancak bazılarının formatı doğru değil.");
                    }
                }
                
                // Her 10 iterasyonda bir ilerlemeyi göster
                if (iteration % 10 == 0 || iteration == MAX_ITERATIONS - 1 || allLessonsPlaced)
                {
                    Console.WriteLine($"Iterasyon {iteration} - Alpha fitness: {alphaWolf.Fitness}, Yerleştirilen dersler: {alphaWolf.Placements.Count}/{totalLessonsToPlace}");
                }
                
                iteration++;
            }
            
            // Alpha kurdun yerleşimlerini yazdır
            Console.WriteLine("\nEn iyi çözüm (Alpha kurt):");
            PrintPlacements(alphaWolf.Placements, data);
            
            // Tüm derslerin doğru formatta yerleştirilip yerleştirilmediğini kontrol et
            bool needsImprovement = false;
            
            // Eğer iterasyonlar sırasında allLessonsPlaced=true olmuşsa, tüm derslerin doğru formatta yerleştirildiğini zaten biliyoruz
            if (allLessonsPlaced)
            {
                Console.WriteLine("\nTüm dersler erken iterasyonda doğru formatta yerleştirildi. İyileştirme gerekmiyor.");
                needsImprovement = false;
            }
            // İterasyonlar erken bitmemişse kontrol yapmaya devam et
            else
            {
                // Yerleştirilmesi gereken toplam ders saatini ve yerleştirilen ders saatini karşılaştır
                if (alphaWolf.Placements.Count < totalLessonsToPlace)
                {
                    needsImprovement = true;
                    Console.WriteLine($"\nYerleştirilen ders sayısı ({alphaWolf.Placements.Count}) toplam ders sayısından ({totalLessonsToPlace}) az. İyileştirme uygulanacak.");
                }
                else
                {
                    // Ders formatı kontrolü
                    foreach (var classCourse in data.ClassCourses.Where(cc => cc.ClassCourseId > 0))
                    {
                        int requiredHours = classCourse.Course.WeeklyHourCount;
                        if (requiredHours <= 0) continue; // Sıfır saatlik dersleri atla
                        
                        var currentCoursePlacements = alphaWolf.Placements
                            .Where(p => p.ClassCourseId == classCourse.ClassCourseId)
                            .ToList();
                        
                        if (currentCoursePlacements.Count != requiredHours)
                        {
                            needsImprovement = true;
                            Console.WriteLine($"{classCourse.Course.CourseName} dersi için yerleştirilen ders saati ({currentCoursePlacements.Count}) gerekli saatten ({requiredHours}) farklı. İyileştirme uygulanacak.");
                            break;
                        }
                        
                        // Format kontrolü
                        if (!string.IsNullOrEmpty(classCourse.Course.PlacementFormat))
                        {
                            List<int> originalFormatBlocks = classCourse.Course.PlacementFormat
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => int.TryParse(p.Trim(), out int val) ? val : 0)
                                .Where(val => val > 0)
                                .ToList();
                            
                            if (originalFormatBlocks.Any() && originalFormatBlocks.Sum() == requiredHours)
                            {
                                var actualBlocks = GetActualPlacedBlocks(currentCoursePlacements, data);
                                originalFormatBlocks.Sort();
                                
                                if (!originalFormatBlocks.SequenceEqual(actualBlocks))
                                {
                                    needsImprovement = true;
                                    Console.WriteLine($"{classCourse.Course.CourseName} dersi için blok formatı uyumsuz. Hedef: {string.Join(',', originalFormatBlocks)}, Mevcut: {string.Join(',', actualBlocks)}. İyileştirme uygulanacak.");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            
            List<PlacementRepresentation> improvedPlacements;
            
            if (needsImprovement)
            {
                // Yerleştirilmeyen veya formatı yanlış olan dersler var, iyileştir
                Console.WriteLine("\nYerleştirilmeyen veya formatı yanlış olan dersler iyileştiriliyor...");
                improvedPlacements = ImproveUnplacedLessons(alphaWolf.Placements, data);
                
                // İyileştirilmiş yerleşimleri yazdır
                Console.WriteLine("\nİyileştirilmiş çözüm:");
                PrintPlacements(improvedPlacements, data);
                
                // Alpha kurdun yerleşimlerini güncelle
                alphaWolf.Placements = improvedPlacements;
            }
            else
            {
                Console.WriteLine("\nTüm dersler doğru formatta yerleştirilmiş. İyileştirme gerekmiyor.");
                improvedPlacements = alphaWolf.Placements;
            }
            
            // Bulunan en iyi çözümü (iyileştirilmiş yerleşimler) ve yerleşimleri dön
            return new Tuple<List<CourseBlock>, List<PlacementRepresentation>>(
                ConvertPlacementsToBlocks(alphaWolf.Placements, lessonBlocks, data),
                alphaWolf.Placements
            );
        }
        
        // Kurtların liderlerini (alpha, beta, delta) günceller
        private void UpdateLeaderWolves(List<Wolf> wolves, ref Wolf alphaWolf, ref Wolf betaWolf, ref Wolf deltaWolf)
        {
            // Fitness'a göre azalan sırada sırala (en yüksek fitness en iyi)
            var sortedWolves = wolves.OrderByDescending(w => w.Fitness).ToList();
            
            // Alpha, Beta ve Delta kurtlarını güncelle
            alphaWolf = new Wolf
            {
                Placements = sortedWolves[0].Placements.Select(p => p.Clone()).ToList(),
                Fitness = sortedWolves[0].Fitness
            };
            
            betaWolf = new Wolf
            {
                Placements = sortedWolves[1].Placements.Select(p => p.Clone()).ToList(),
                Fitness = sortedWolves[1].Fitness
            };
            
            deltaWolf = new Wolf
            {
                Placements = sortedWolves[2].Placements.Select(p => p.Clone()).ToList(),
                Fitness = sortedWolves[2].Fitness
            };
        }
        
        // Yerleşimleri ders bloklarına dönüştür
        private List<CourseBlock> ConvertPlacementsToBlocks(List<PlacementRepresentation> placements, List<CourseBlock> originalBlocks, SchedulingData data)
        {
            // Yerleştirilmiş tüm blokları kapsayan bir liste oluştur
            Dictionary<int, int> classCourseCount = new Dictionary<int, int>();
            
            // Her bir yerleşimin için hangi blok için olduğunu takip et
            foreach (var placement in placements)
            {
                if (!classCourseCount.ContainsKey(placement.ClassCourseId))
                {
                    classCourseCount[placement.ClassCourseId] = 0;
                }
                classCourseCount[placement.ClassCourseId]++;
            }
            
            // Orijinal blokları kopyala
            var resultBlocks = new List<CourseBlock>();
            
            // Her bir blok için, yerleştirilen ders sayısını kontrol et
            foreach (var block in originalBlocks)
            {
                int classId = block.ClassCourse.ClassCourseId;
                
                // Eğer bu blok için en az bir yerleştirme yapıldıysa, bloğu sonuç listesine ekle
                if (classCourseCount.ContainsKey(classId) && classCourseCount[classId] > 0)
                {
                    resultBlocks.Add(block);
                }
            }
            
            return resultBlocks;
        }
        
        // Yerleşimleri TimetablePlacement modellerine dönüştür
        private List<TimetablePlacement> ConvertToTimetablePlacements(List<PlacementRepresentation> placements, SchedulingData data, int timetableId)
        {
            var timetablePlacements = new List<TimetablePlacement>();
            int skippedPlacements = 0;
            
            Console.WriteLine($"\nToplam dönüştürülecek yerleşim sayısı: {placements.Count}");
            
            foreach (var placement in placements)
            {
                var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == placement.ClassCourseId);
                if (classCourse == null) 
                {
                    Console.WriteLine($"HATA: ClassCourseId={placement.ClassCourseId} bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                var day = data.Days.FirstOrDefault(d => d.DayId == placement.DayId);
                if (day == null) 
                {
                    Console.WriteLine($"HATA: DayId={placement.DayId} bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                var lesson = data.Lessons.FirstOrDefault(l => l.LessonId == placement.LessonId);
                if (lesson == null) 
                {
                    Console.WriteLine($"HATA: LessonId={placement.LessonId} bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                var classEntity = classCourse.Class;
                var course = classCourse.Course;
                var educator = classCourse.Educator;
                var classroom = classCourse.Classroom;
                
                // Gerekli tüm verilerin var olduğundan emin olalım
                if (classEntity == null) 
                {
                    Console.WriteLine($"HATA: ClassCourseId={placement.ClassCourseId} için Class nesnesi bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                if (course == null) 
                {
                    Console.WriteLine($"HATA: ClassCourseId={placement.ClassCourseId} için Course nesnesi bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                if (educator == null) 
                {
                    Console.WriteLine($"HATA: ClassCourseId={placement.ClassCourseId} için Educator nesnesi bulunamadı.");
                    skippedPlacements++;
                    continue;
                }
                
                if (classroom == null) 
                {
                    Console.WriteLine($"HATA: ClassCourseId={placement.ClassCourseId} için Classroom nesnesi bulunamadı. Sınıf: {classEntity.ClassName}, Ders: {course.CourseName}");
                    skippedPlacements++;
                    continue;
                }
                
                var timetablePlacement = new TimetablePlacement
                {
                    TimetableId = timetableId,
                    WorkspaceId = day.WorkspaceId,
                    DayId = day.DayId,
                    DayOfWeek = day.DayOfWeek,
                    DayShortName = day.ShortName,
                    LessonNumber = lesson.LessonNumber,
                    StartTime = lesson.StartTime,
                    EndTime = lesson.EndTime,
                    CourseCode = course.CourseCode,
                    CourseName = course.CourseName,
                    ClassroomName = classroom.ClassroomName,
                    ClassName = classEntity.ClassName,
                    EducatorFullName = $"{educator.Title} {educator.FirstName} {educator.LastName}",
                    EducatorShortName = educator.ShortName
                };
                
                timetablePlacements.Add(timetablePlacement);
            }
            
            Console.WriteLine($"Toplam atılan yerleşim sayısı: {skippedPlacements}");
            Console.WriteLine($"Veritabanına kaydedilecek yerleşim sayısı: {timetablePlacements.Count}");
            
            return timetablePlacements;
        }
        
        // Yerleştirilemeyen dersleri iyileştir
        private List<PlacementRepresentation> ImproveUnplacedLessons(List<PlacementRepresentation> placements, SchedulingData data)
        {
            var improvedPlacements = new List<PlacementRepresentation>(placements); // Mevcut yerleşimlerin kopyası ile başla

            var fixedPlacementsByCourse = data.TimetableConstraints
                .Where(tc => tc.ClassCourseId > 0)
                .GroupBy(tc => tc.ClassCourseId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var classCourse in data.ClassCourses.OrderBy(cc => _random.Next()))
            {
                if (classCourse.ClassCourseId == 0) continue; // "CLOSED" olarak işaretlenen dersleri atla

                int requiredHours = classCourse.Course.WeeklyHourCount;
                var currentCoursePlacements = improvedPlacements
                    .Where(p => p.ClassCourseId == classCourse.ClassCourseId)
                    .ToList();
                int currentScheduledHours = currentCoursePlacements.Count;

                List<int> originalFormatBlocks = new List<int>();
                if (!string.IsNullOrEmpty(classCourse.Course.PlacementFormat))
                {
                    originalFormatBlocks = classCourse.Course.PlacementFormat
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => int.TryParse(p.Trim(), out int val) ? val : 0)
                        .Where(val => val > 0)
                        .ToList();
                }
                if (!originalFormatBlocks.Any() || originalFormatBlocks.Sum() != requiredHours)
                {
                    originalFormatBlocks.Clear();
                    for (int i = 0; i < requiredHours; i++) originalFormatBlocks.Add(1);
                }
                originalFormatBlocks.Sort();

                var actualCurrentBlocks = GetActualPlacedBlocks(currentCoursePlacements, data);
                // actualCurrentBlocks zaten GetActualPlacedBlocks tarafından sıralanmış

                bool needsImprovement = false;
                if (currentScheduledHours != requiredHours)
                {
                    needsImprovement = true;
                    Console.WriteLine($"İyileştirme (Saat Eksik/Fazla): {classCourse.Class.ClassName}, Ders: {classCourse.Course.CourseName} - Gerekli: {requiredHours}, Yerleştirilmiş: {currentScheduledHours}");
                }
                else if (!originalFormatBlocks.SequenceEqual(actualCurrentBlocks))
                {
                    needsImprovement = true;
                    Console.WriteLine($"İyileştirme (Format Farklı): {classCourse.Class.ClassName}, Ders: {classCourse.Course.CourseName} - Hedef: {string.Join(',', originalFormatBlocks)}, Mevcut: {string.Join(',', actualCurrentBlocks)}");
                }

                if (needsImprovement)
                {
                    var fixedPlacementsForThisCourse = fixedPlacementsByCourse.ContainsKey(classCourse.ClassCourseId)
                        ? fixedPlacementsByCourse[classCourse.ClassCourseId]
                        : new List<TimetableConstraint>();

                    improvedPlacements.RemoveAll(p =>
                        p.ClassCourseId == classCourse.ClassCourseId &&
                        !fixedPlacementsForThisCourse.Any(fp => fp.DayId == p.DayId && fp.LessonId == p.LessonId && fp.ClassCourseId == p.ClassCourseId)); // Aynı dersin sabit yerleşimi olduğundan emin ol

                    currentCoursePlacements = improvedPlacements // Kaldırma işleminden sonra mevcut yerleşimleri yenile
                        .Where(p => p.ClassCourseId == classCourse.ClassCourseId)
                        .ToList();

                    List<int> scheduledFixedBlocks = IdentifyScheduledBlocks(
                        classCourse.ClassCourseId,
                        new List<int>(originalFormatBlocks), // IdentifyScheduledBlocks'a orijinal formatın bir kopyasını aktar
                        fixedPlacementsForThisCourse,
                        data);

                    List<int> blocksToPlace = new List<int>(originalFormatBlocks);
                    var tempScheduledFixedBlocks = new List<int>(scheduledFixedBlocks); // Kaldırma işlemi için değiştirilebilir kopya
                    foreach (var block in originalFormatBlocks)
                    {
                        if (tempScheduledFixedBlocks.Contains(block))
                        {
                            blocksToPlace.Remove(block);
                            tempScheduledFixedBlocks.Remove(block);
                        }
                    }
                    blocksToPlace = blocksToPlace.OrderByDescending(b => b).ToList();

                    Console.WriteLine($"İyileştirme - Yerleştirilecek Bloklar ({classCourse.Course.CourseName}): {string.Join(',', blocksToPlace)}");

                    foreach (var blockToPlace in blocksToPlace)
                    {
                        if (blockToPlace <= 0) continue;
                        bool blockPlacedSuccessfully = false;

                        var randomizedDays = data.Days
                            .Where(d => d.LessonCount >= blockToPlace)
                            .OrderBy(d => _random.Next())
                            .ToList();

                        foreach (var day in randomizedDays)
                        {
                            if (blockPlacedSuccessfully) break;

                            var possibleStartLessons = data.Lessons
                                .Where(l => l.LessonNumber + blockToPlace - 1 <= day.LessonCount)
                                .OrderBy(l => _random.Next())
                                .ToList();

                            foreach (var lesson in possibleStartLessons)
                            {
                                if (IsValidPlacement(classCourse, day.DayId, lesson.LessonId, blockToPlace, data, improvedPlacements))
                                {
                                    for (int i = 0; i < blockToPlace; i++)
                                    {
                                        improvedPlacements.Add(new PlacementRepresentation
                                        {
                                            ClassCourseId = classCourse.ClassCourseId,
                                            DayId = day.DayId,
                                            LessonId = lesson.LessonId + i
                                        });
                                    }
                                    blockPlacedSuccessfully = true;
                                    Console.WriteLine($"İyileştirme - Başarıyla Yerleştirildi: {classCourse.Course.CourseName}, Blok: {blockToPlace}, Gün: {day.ShortName}, Saat: {lesson.LessonNumber}");
                                    break;
                                }
                            }
                        }
                        if (!blockPlacedSuccessfully)
                        {
                            Console.WriteLine($"İyileştirme - HATA: {classCourse.Course.CourseName} için {blockToPlace} saatlik blok yerleştirilemedi!");
                        }
                    }
                }
            }
            return improvedPlacements;
        }
        
        // Yerleşimin geçerli olup olmadığını kontrol et
        private bool IsValidPlacement(ClassCourse classCourse, int dayId, int startLessonId, int blockSize, SchedulingData data, List<PlacementRepresentation> existingPlacements)
        {
            // Belirtilen gün için dersleri al
            var dayLessons = data.Lessons
                .Where(l => l.LessonId >= startLessonId && l.LessonId < startLessonId + blockSize)
                .OrderBy(l => l.LessonNumber)
                .ToList();

            // Blok boyutu için yeterli ardışık ders olduğundan emin ol
            if (dayLessons.Count != blockSize) return false;

            // Derslerin ardışık olduğundan emin ol
            for (int i = 1; i < dayLessons.Count; i++)
            {
                if (dayLessons[i].LessonNumber != dayLessons[i - 1].LessonNumber + 1)
                    return false;
            }

            // Sınıf, eğitimci ve derslik için herhangi bir çakışma var mı kontrol et
            for (int i = 0; i < blockSize; i++)
            {
                int currentLessonId = startLessonId + i;

                // 1. Sınıf için çakışma kontrolü
                if (existingPlacements.Any(p =>
                    p.DayId == dayId &&
                    p.LessonId == currentLessonId &&
                    data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.ClassId == classCourse.ClassId)))
                    return false;

                // 2. Eğitimci için çakışma kontrolü
                if (existingPlacements.Any(p =>
                    p.DayId == dayId &&
                    p.LessonId == currentLessonId &&
                    data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.EducatorId == classCourse.EducatorId)))
                    return false;

                // 3. Derslik için çakışma kontrolü
                if (classCourse.ClassroomId != 0 && existingPlacements.Any(p =>
                    p.DayId == dayId &&
                    p.LessonId == currentLessonId &&
                    data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.ClassroomId == classCourse.ClassroomId)))
                    return false;

                // 4. "Kapalı" kısıtlama ile çakışma
                if (existingPlacements.Any(p =>
                    p.DayId == dayId &&
                    p.LessonId == currentLessonId &&
                    p.ClassCourseId == 0)) // ClassCourseId=0 "kapalı" yerleri belirtir
                    return false;
            }

            return true;
        }
        
        // Mevcut yerleşimlerden blokları tanımla
        private List<int> IdentifyScheduledBlocks(int classCourseId, List<int> originalFormatBlocks, List<TimetableConstraint> fixedPlacements, SchedulingData data)
        {
            List<int> scheduledBlocks = new List<int>();

            // Sınıf için gün ve ders bilgilerini al
            var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == classCourseId);
            if (classCourse == null) return scheduledBlocks;

            // Ders yerleşimlerini grupla (ardışık saatleri blok olarak belirlemek için)
            var orderedPlacements = fixedPlacements
                .OrderBy(p => p.DayId)
                .ThenBy(p => data.Lessons.FirstOrDefault(l => l.LessonId == p.LessonId)?.LessonNumber ?? 0)
                .ToList();

            // Ardışık dersleri takip etmek için geçici değişkenler
            int currentDayId = -1;
            int lastLessonNumber = -1;
            int currentBlockSize = 0;

            foreach (var placement in orderedPlacements)
            {
                var lesson = data.Lessons.FirstOrDefault(l => l.LessonId == placement.LessonId);
                if (lesson == null) continue;

                if (currentDayId != placement.DayId || lesson.LessonNumber != lastLessonNumber + 1)
                {
                    // Yeni bir blok başlattık, önceki bloğu kaydet
                    if (currentBlockSize > 0)
                        scheduledBlocks.Add(currentBlockSize);

                    // Yeni blok başlat
                    currentBlockSize = 1;
                    currentDayId = placement.DayId;
                    lastLessonNumber = lesson.LessonNumber;
                }
                else
                {
                    // Mevcut bloğu genişlet
                    currentBlockSize++;
                    lastLessonNumber = lesson.LessonNumber;
                }
            }

            // Son bloğu ekle
            if (currentBlockSize > 0)
                scheduledBlocks.Add(currentBlockSize);

            return scheduledBlocks;
        }
        
        // Yerleştirilmiş derslerin blok yapısını hesapla
        private List<int> GetActualPlacedBlocks(List<PlacementRepresentation> placements, SchedulingData data)
        {
            List<int> actualBlocks = new List<int>();
            if (!placements.Any()) return actualBlocks;

            // Yerleşimleri gün ve ders saatine göre sırala
            var orderedPlacements = placements
                .OrderBy(p => p.DayId)
                .ThenBy(p => data.Lessons.FirstOrDefault(l => l.LessonId == p.LessonId)?.LessonNumber ?? 0)
                .ToList();

            int currentDayId = orderedPlacements[0].DayId;
            int lastLessonNumber = -1;
            int currentBlockSize = 0;

            foreach (var placement in orderedPlacements)
            {
                var lesson = data.Lessons.FirstOrDefault(l => l.LessonId == placement.LessonId);
                if (lesson == null) continue;

                if (currentDayId != placement.DayId || lesson.LessonNumber != lastLessonNumber + 1)
                {
                    // Yeni bir blok başlattık, önceki bloğu kaydet
                    if (currentBlockSize > 0)
                        actualBlocks.Add(currentBlockSize);

                    // Yeni blok başlat
                    currentBlockSize = 1;
                    currentDayId = placement.DayId;
                    lastLessonNumber = lesson.LessonNumber;
                }
                else
                {
                    // Mevcut bloğu genişlet
                    currentBlockSize++;
                    lastLessonNumber = lesson.LessonNumber;
                }
            }

            // Son bloğu ekle
            if (currentBlockSize > 0)
                actualBlocks.Add(currentBlockSize);

            actualBlocks.Sort(); // Karşılaştırma için tutarlı sıralama
            return actualBlocks;
        }
        
        // Yerleşimleri veritabanına kaydet
        private async Task SavePlacementsAsync(List<PlacementRepresentation> placements, Timetable timetable, SchedulingData data)
        {
            // Yerleşimleri TimetablePlacement modellerine dönüştür
            var timetablePlacements = ConvertToTimetablePlacements(placements, data, timetable.TimetableId);
            
            // Önce var olan yerleşimleri sil
            var existingPlacements = await _context.TimetablePlacements
                .Where(tp => tp.TimetableId == timetable.TimetableId)
                .ToListAsync();
                
            _context.TimetablePlacements.RemoveRange(existingPlacements);
            
            // Yeni yerleşimleri ekle
            await _context.TimetablePlacements.AddRangeAsync(timetablePlacements);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"{timetablePlacements.Count} adet yerleşim başarıyla kaydedildi.");
        }
        
        // GWO algoritmasına göre yeni pozisyon (yerleşimler) hesapla
        private List<PlacementRepresentation> CalculateNewPosition(Wolf currentWolf, Wolf alphaWolf, Wolf betaWolf, Wolf deltaWolf, double a, List<CourseBlock> lessonBlocks, SchedulingData data)
        {
            // Mevcut yerleşimleri temel alarak yeni bir çözüm oluşturma stratejisi
            // Rastgele bir süreklilik oranı belirle - mevcut çözümün ne kadarının korunacağı
            double continuityRate = 0.5 + (_random.NextDouble() * 0.3); // 0.5 ile 0.8 arası
            
            // Mevcut yerleşimlerin bir kısmını koruyacağız
            int placementsToKeep = (int)(currentWolf.Placements.Count * continuityRate);
            
            // Korunacak yerleşimleri rastgele seç
            var placementsToKeepList = currentWolf.Placements
                .OrderBy(x => _random.Next())
                .Take(placementsToKeep)
                .ToList();
            
            // Korunan yerleşimlere karşılık gelen ClassCourseId'leri belirle
            var keptClassCourseIds = placementsToKeepList
                .Select(p => p.ClassCourseId)
                .Distinct()
                .ToList();
            
            // Yerleştirilmesi gereken ders bloklarını belirle (henüz yerleştirilmemiş olanlar)
            var blocksToPlace = lessonBlocks
                .Where(block => !keptClassCourseIds.Contains(block.ClassCourse.ClassCourseId))
                .ToList();
                
            // Alpha, beta ve delta kurtlardan öğrenme - bu kurtların çözümlerinden ilham al
            var leaderPlacements = new List<PlacementRepresentation>();
            
            // Alpha, beta ve delta'nın yerleşimlerinden ilham al
            // Her blok için lider kurtların yerleşimlerini kontrol et
            foreach (var block in blocksToPlace)
            {
                int classCourseId = block.ClassCourse.ClassCourseId;
                
                // Öncelikle alpha'nın çözümüne bak
                var alphaPlacements = alphaWolf.Placements
                    .Where(p => p.ClassCourseId == classCourseId)
                    .ToList();
                    
                if (alphaPlacements.Any() && _random.NextDouble() < 0.5) // %50 ihtimalle alpha'dan al
                {
                    leaderPlacements.AddRange(alphaPlacements);
                    continue;
                }
                
                // Beta'nın çözümüne bak
                var betaPlacements = betaWolf.Placements
                    .Where(p => p.ClassCourseId == classCourseId)
                    .ToList();
                    
                if (betaPlacements.Any() && _random.NextDouble() < 0.4) // %40 ihtimalle beta'dan al
                {
                    leaderPlacements.AddRange(betaPlacements);
                    continue;
                }
                
                // Delta'nın çözümüne bak
                var deltaPlacements = deltaWolf.Placements
                    .Where(p => p.ClassCourseId == classCourseId)
                    .ToList();
                    
                if (deltaPlacements.Any() && _random.NextDouble() < 0.3) // %30 ihtimalle delta'dan al
                {
                    leaderPlacements.AddRange(deltaPlacements);
                    continue;
                }
                
                // Liderlerden alamadıysak, yeni bir yerleşim bul
                var possiblePlacements = FindPossiblePlacements(block, data, placementsToKeepList.Concat(leaderPlacements).ToList());
                
                if (possiblePlacements.Count > 0)
                {
                    var selectedPlacement = possiblePlacements[_random.Next(possiblePlacements.Count)];
                    
                    // Gün ve ders saatini al
                    int dayId = selectedPlacement.DayId;
                    int lessonId = selectedPlacement.LessonId;
                    
                    // Bu gün için geçerli olan dersleri al ve LessonNumber'a göre sırala
                    var dayLessons = data.Lessons
                        .OrderBy(l => l.LessonNumber)
                        .ToList();
                    
                    // Başlangıç ders saati indeksini bulalım
                    int startIndex = -1;
                    for (int i = 0; i < dayLessons.Count; i++)
                    {
                        if (dayLessons[i].LessonId == lessonId)
                        {
                            startIndex = i;
                            break;
                        }
                    }
                    
                    if (startIndex != -1)
                    {
                        // BlockSize kadar ardışık ders saati için yerleşim ekle
                        for (int i = 0; i < block.BlockSize; i++)
                        {
                            // Ders listesi dışına çıkmadığımızdan emin olalım
                            if (startIndex + i >= dayLessons.Count) 
                                break;
                                
                            leaderPlacements.Add(new PlacementRepresentation
                            {
                                ClassCourseId = classCourseId,
                                DayId = dayId,
                                LessonId = dayLessons[startIndex + i].LessonId
                            });
                        }
                    }
                }
            }
            
            // Yeni yerleşimler = korunan yerleşimler + liderlerden ve yeni hesaplanan yerleşimler
            return placementsToKeepList.Concat(leaderPlacements).ToList();
        }
        
        // Bir kurdun çözümünün (yerleşimlerin) uygunluğunu hesaplar
        // Yüksek değer daha iyi çözüm anlamına gelir
        private double CalculateFitness(List<PlacementRepresentation> placements, List<CourseBlock> lessonBlocks, SchedulingData data)
        {
            // Başlangıç fitness değeri - bu değerden ceza puanları çıkarılacak
            double fitness = 1000.0;
            
            // 1. Yerleştirilmeyen derslerin sayısını hesapla
            // Ders bloklarının toplam boyutu
            int blockLessonsCount = lessonBlocks.Sum(block => block.BlockSize);
            
            // TimetableConstraint ile önceden yerleştirilmiş dersleri say
            int preplacedLessonsCount = data.TimetableConstraints.Count;
            
            // Toplam yerleştirilmesi gereken ders sayısı
            int totalLessonsToPlace = blockLessonsCount + preplacedLessonsCount;
            int placedLessons = placements.Count;
            int unplacedLessons = totalLessonsToPlace - placedLessons;
            
            // Yerleştirilmeyen her ders için 100 puan ceza
            fitness -= unplacedLessons * 100.0;
            
            // Eğer hiç ders yerleştirilmemişse, fitness 0 olmalı
            if (placedLessons == 0)
                return 0;
                
            // 2. Çakışmaları kontrol et
            int conflicts = 0;
            
            // Eğitimci çakışmaları
            var educatorConflicts = placements
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.First(cc => cc.ClassCourseId == p.ClassCourseId).EducatorId).Distinct().Count() < g.Count())
                .Count();
            conflicts += educatorConflicts;
            
            // Sınıf çakışmaları
            var classConflicts = placements
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.First(cc => cc.ClassCourseId == p.ClassCourseId).ClassId).Distinct().Count() < g.Count())
                .Count();
            conflicts += classConflicts;
            
            // Derslik çakışmaları
            var classroomConflicts = placements
                .Where(p => data.ClassCourses.First(cc => cc.ClassCourseId == p.ClassCourseId).ClassroomId != 0)
                .GroupBy(p => new { p.DayId, p.LessonId })
                .Where(g => g.Select(p => data.ClassCourses.First(cc => cc.ClassCourseId == p.ClassCourseId).ClassroomId).Distinct().Count() < g.Count())
                .Count();
            conflicts += classroomConflicts;
            
            // Çakışma başına 50 puan ceza
            fitness -= conflicts * 50.0;
            
            // 3. Ders dağılımını değerlendir (dersler günlere dengeli dağıtılmış mı?)
            // Her sınıf için günlere göre ders sayısını hesapla
            var classDayDistribution = placements
                .GroupBy(p => new { 
                    ClassId = data.ClassCourses.First(cc => cc.ClassCourseId == p.ClassCourseId).ClassId,
                    p.DayId
                })
                .ToDictionary(g => g.Key, g => g.Count());
                
            // Standart sapma hesapla - düşük standart sapma daha iyi dağılım demektir
            double distributionPenalty = 0.0;
            
            foreach (var classId in data.ClassCourses.Select(cc => cc.ClassId).Distinct())
            {
                var classPlacements = classDayDistribution
                    .Where(kv => kv.Key.ClassId == classId)
                    .ToDictionary(kv => kv.Key.DayId, kv => kv.Value);
                    
                if (classPlacements.Count > 0)
                {
                    double mean = classPlacements.Values.Average();
                    double variance = classPlacements.Values.Select(v => Math.Pow(v - mean, 2)).Sum() / classPlacements.Count;
                    double stdDev = Math.Sqrt(variance);
                    
                    distributionPenalty += stdDev * 5.0; // Dağılım cezasını ayarla
                }
            }
            
            fitness -= distributionPenalty;
            
            // Fitness değeri negatif olmamalı
            return Math.Max(0, fitness);
        }
        
       
        private List<PlacementRepresentation> GenerateRandomSolution(List<CourseBlock> lessonBlocks, SchedulingData data)
        {
            var placements = new List<PlacementRepresentation>();
            //var shuffledBlocks = lessonBlocks.OrderBy(x => _random.Next()).ToList(); // Ders bloklarını karıştır
            
            // Öncelikle TimetableConstraints'teki mevcut yerleşimleri ekle
            foreach (var constraint in data.TimetableConstraints)
            {
                // ClassCourseId=0 olan kısıtlamalar "kapalı" yerleri belirtir, bunları yok sayabiliriz
                if (constraint.ClassCourseId > 0)
                {
                    var placement = new PlacementRepresentation
                    {
                        ClassCourseId = constraint.ClassCourseId,
                        DayId = constraint.DayId,
                        LessonId = constraint.LessonId
                    };
                    
                    placements.Add(placement);
                }
            }
            
            // Her bir ders bloğu için
            foreach (var block in lessonBlocks)
            {
                var possiblePlacements = FindPossiblePlacements(block, data, placements);
                
                if (possiblePlacements.Count > 0)
                {
                    // Mevcut yerleşimlerden rastgele birini seç
                    int randomIndex = _random.Next(possiblePlacements.Count);
                    var selectedPlacement = possiblePlacements[randomIndex];
                    
                    // Seçilen gündeki dersleri LessonNumber'a göre sıralayarak al
                    var dayId = selectedPlacement.DayId;
                    var orderedLessons = data.Lessons
                        .OrderBy(l => l.LessonNumber)
                        .ToList();
                    
                    // Başlangıç ders saati indeksini bulalım
                    int startIndex = -1;
                    for (int i = 0; i < orderedLessons.Count; i++)
                    {
                        if (orderedLessons[i].LessonId == selectedPlacement.LessonId)
                        {
                            startIndex = i;
                            break;
                        }
                    }
                    
                    if (startIndex == -1)
                    {
                        Console.WriteLine($"HATA: LessonId={selectedPlacement.LessonId} bulunamadı!");
                        continue;
                    }
                    
                    // BlockSize kadar ardışık ders saati için yerleşim ekle
                    for (int i = 0; i < block.BlockSize; i++)
                    {
                        // Ders listesi dışına çıkmadığımızdan emin olalım
                        if (startIndex + i >= orderedLessons.Count) 
                            break;
                            
                        var placement = new PlacementRepresentation
                        {
                            ClassCourseId = block.ClassCourse.ClassCourseId,
                            DayId = dayId,
                            LessonId = orderedLessons[startIndex + i].LessonId // Ardışık ders saatlerinin doğru LessonId'lerini kullan
                        };
                        
                        placements.Add(placement);
                    }
                }
                // Eğer hiç uygun yer bulunamazsa, bu blok yerleştirilmez ve sonraki bloğa geçilir
            }
            
            // Yerleşimleri konsola yazdır
            PrintPlacements(placements, data);
            
            return placements;
        }
        
        private void PrintPlacements(List<PlacementRepresentation> placements, SchedulingData data)
        {
            Console.WriteLine("====== Yerleşimler ======");
            Console.WriteLine($"Toplam Yerleştirilen Ders Sayısı: {placements.Count}");
            
            // Yerleşimleri gün ve saate göre sırala
            var orderedPlacements = placements
                .OrderBy(p => p.DayId)
                .ThenBy(p => p.LessonId)
                .ToList();
            
            foreach (var placement in orderedPlacements)
            {
                // ClassCourse'u bul
                var classCourse = data.ClassCourses.FirstOrDefault(cc => cc.ClassCourseId == placement.ClassCourseId);
                if (classCourse == null) continue;
                
                // Gün ve ders saati bilgilerini bul
                var day = data.Days.FirstOrDefault(d => d.DayId == placement.DayId);
                
                // Ders saati bilgisi için daha güçlü bir kontrol
                var lesson = data.Lessons.FirstOrDefault(l => l.LessonId == placement.LessonId);
                if (lesson == null)
                {
                    // Debug bilgisi
                    Console.WriteLine($"HATA: LessonId={placement.LessonId} için ders saati bulunamadı!");
                    Console.WriteLine($"Mevcut ders saati IDleri: {string.Join(", ", data.Lessons.Select(l => l.LessonId))}");
                }
                
                string dayName = day?.DayOfWeek ?? "Bilinmeyen Gün";
                string lessonName = lesson != null ? $"Ders {lesson.LessonNumber} ({lesson.StartTime}-{lesson.EndTime})" : $"LessonId: {placement.LessonId} - Bulunamadı!";
                
                // Eğitimci, sınıf, ders ve derslik bilgileri
                string className = classCourse.Class?.ClassName ?? "Bilinmeyen Sınıf";
                string courseName = classCourse.Course?.CourseName ?? "Bilinmeyen Ders";
                string educatorName = classCourse.Educator != null ? 
                    $"{classCourse.Educator.Title} {classCourse.Educator.FirstName} {classCourse.Educator.LastName}" : 
                    "Bilinmeyen Eğitimci";
                string classroomName = classCourse.Classroom?.ClassroomName ?? "Bilinmeyen Derslik";
                
                Console.WriteLine($"Gün: {dayName}, Saat: {lessonName}, Sınıf: {className}, Ders: {courseName}, Eğitimci: {educatorName}, Derslik: {classroomName}");
            }
            
            Console.WriteLine("==========================\n");
        }
        
        private List<Wolf> InitializeWolfPopulation(List<CourseBlock> lessonBlocks, SchedulingData data, int populationSize)
        {
            var population = new List<Wolf>();
            
            for (int i = 0; i < populationSize; i++)
            {
                var wolf = new Wolf
                {
                    Placements = GenerateRandomSolution(lessonBlocks, data),
                    Fitness = 0.0
                };
                
                population.Add(wolf);
            }
            
            return population;
        }
        
        private List<PlacementRepresentation> FindPossiblePlacements(CourseBlock block, SchedulingData data, List<PlacementRepresentation> existingPlacements)
        {
            var possiblePlacements = new List<PlacementRepresentation>();
            
            // Eğitimci, sınıf ve derslik bilgilerini al
            int educatorId = block.ClassCourse.EducatorId;
            int classId = block.ClassCourse.ClassId;
            int? classroomId = block.ClassCourse.ClassroomId;
            int blockSize = block.BlockSize;
            
            // Her gün ve ders saati için kontrol et
            foreach (var day in data.Days)
            {
                // LessonCount = 0 olan günlere ders yerleştirme
                if (day.LessonCount == 0)
                    continue;
                    
                // Bu gün için geçerli olan dersleri al ve LessonNumber'a göre sırala
                var dayLessons = data.Lessons
                    .OrderBy(l => l.LessonNumber)
                    .ToList();
                    
                // Eğer günde ders sayısından fazla ders varsa, sadece gün için belirlenen ders sayısı kadar al
                if (dayLessons.Count > day.LessonCount)
                    dayLessons = dayLessons.Take(day.LessonCount).ToList();
                    
                // Blok eğer gündeki ders sayısından büyükse, bu güne yerleştiremeyiz
                if (blockSize > dayLessons.Count)
                    continue;
                
                // Bloğun yerleştirilebileceği tüm başlangıç noktalarını kontrol et
                for (int lessonStartIndex = 0; lessonStartIndex <= dayLessons.Count - blockSize; lessonStartIndex++)
                {
                    bool isValidPlacement = true;
                    
                    // Bu konumda blockSize kadar ardışık ders saati için kontrol et
                    for (int i = 0; i < blockSize; i++)
                    {
                        var currentLesson = dayLessons[lessonStartIndex + i];
                        int currentLessonId = currentLesson.LessonId;
                        
                        // 1. Eğitimci kısıtlamalarını kontrol et
                        if (data.EducatorConstraints.Any(ec => 
                            ec.EducatorId == educatorId && 
                            ec.DayId == day.DayId && 
                            ec.LessonId == currentLessonId))
                        {
                            isValidPlacement = false;
                            break;
                        }
                        
                        // 2. Sınıf kısıtlamalarını kontrol et
                        if (data.ClassConstraints.Any(cc => 
                            cc.ClassId == classId && 
                            cc.DayId == day.DayId && 
                            cc.LessonId == currentLessonId))
                        {
                            isValidPlacement = false;
                            break;
                        }
                        
                        // 3. Derslik kısıtlamalarını kontrol et (eğer derslik belirtilmişse)
                        if (classroomId.HasValue && data.ClassroomConstraints.Any(crc => 
                            crc.ClassroomId == classroomId.Value && 
                            crc.DayId == day.DayId && 
                            crc.LessonId == currentLessonId))
                        {
                            isValidPlacement = false;
                            break;
                        }
                        
                        // 4. Mevcut yerleşimlerle çakışma kontrol et - eğitimci için
                        if (existingPlacements.Any(p => 
                            // Aynı eğitimci aynı zamanda başka bir derste olamaz
                            p.DayId == day.DayId && 
                            p.LessonId == currentLessonId &&
                            data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.EducatorId == educatorId)))
                        {
                            isValidPlacement = false;
                            break;
                        }
                        
                        // 5. Mevcut yerleşimlerle çakışma kontrol et - sınıf için
                        if (existingPlacements.Any(p => 
                            // Aynı sınıf aynı zamanda başka bir derste olamaz
                            p.DayId == day.DayId && 
                            p.LessonId == currentLessonId &&
                            data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.ClassId == classId)))
                        {
                            isValidPlacement = false;
                            break;
                        }
                        
                        // 6. Mevcut yerleşimlerle çakışma kontrol et - derslik için
                        if (classroomId.HasValue && existingPlacements.Any(p => 
                            // Aynı derslik aynı zamanda başka bir ders için kullanılamaz
                            p.DayId == day.DayId && 
                            p.LessonId == currentLessonId &&
                            data.ClassCourses.Any(cc => cc.ClassCourseId == p.ClassCourseId && cc.ClassroomId == classroomId.Value)))
                        {
                            isValidPlacement = false;
                            break;
                        }
                    }
                    
                    // Tüm koşulları sağlıyorsa, bu yerleşim olanaklı yerleşimler listesine ekle
                    if (isValidPlacement)
                    {
                        possiblePlacements.Add(new PlacementRepresentation
                        {
                            ClassCourseId = block.ClassCourse.ClassCourseId,
                            DayId = day.DayId,
                            LessonId = dayLessons[lessonStartIndex].LessonId // Doğru Lesson ID'yi kullan
                        });
                    }
                }
            }
            
            return possiblePlacements;
        }

        // Tüm dersleri önem derecesine göre sıralayıp ders bloklarına dönüştürür ve döndürür
        private List<CourseBlock> CreateSortedLessonBlocks(SchedulingData data)
        {
            // Sonuç listesi
            List<CourseBlock> lessonBlocks = new List<CourseBlock>();
            
            // Her ClassCourse için eğitimci, sınıf ve derslik kullanım sayılarını hesapla
            Dictionary<int, int> educatorUsageCount = new Dictionary<int, int>();
            Dictionary<int, int> classUsageCount = new Dictionary<int, int>();
            Dictionary<int, int> classroomUsageCount = new Dictionary<int, int>();
            
            // Kullanım sayılarını hesapla
            foreach (var classCourse in data.ClassCourses)
            {
                // Course içindeki haftalık ders saati
                int weeklyHours = classCourse.Course.WeeklyHourCount;
                
                // Eğitimci kullanım sayısını güncelle
                if (!educatorUsageCount.ContainsKey(classCourse.EducatorId))
                    educatorUsageCount[classCourse.EducatorId] = 0;
                educatorUsageCount[classCourse.EducatorId] += weeklyHours;
                
                // Sınıf kullanım sayısını güncelle
                if (!classUsageCount.ContainsKey(classCourse.ClassId))
                    classUsageCount[classCourse.ClassId] = 0;
                classUsageCount[classCourse.ClassId] += weeklyHours;
                
                // Derslik kullanım sayısını güncelle
                if (!classroomUsageCount.ContainsKey(classCourse.ClassroomId))
                    classroomUsageCount[classCourse.ClassroomId] = 0;
                classroomUsageCount[classCourse.ClassroomId] += weeklyHours;
            }
            
            // ClassCourse'ları kullanım sayılarına göre sırala
            // Önce en çok kullanılan eğitimci, sınıf veya dersliğe sahip olanlar
            var sortedClassCourses = data.ClassCourses
                .OrderByDescending(cc => 
                    educatorUsageCount.GetValueOrDefault(cc.EducatorId, 0) + 
                    classUsageCount.GetValueOrDefault(cc.ClassId, 0) + 
                    classroomUsageCount.GetValueOrDefault(cc.ClassroomId, 0))
                .ToList();
            
            // TimetableConstraint'te yer alan ClassCourse ID'leri - bunları listelemeye gerek yok
            var assignedClassCourseIds = data.TimetableConstraints
                .Where(tc => tc.ClassCourseId > 0) // 0 olan ID'ler "kapalı" yerleri belirtir
                .Select(tc => tc.ClassCourseId)
                .Distinct()
                .ToHashSet();
                
            // Sıralanmış her ClassCourse için ders bloklarını oluştur ve listeye ekle
            foreach (var classCourse in sortedClassCourses)
            {
                // Eğer bu ders için TimetableConstraint ile yer belirlenmişse, atla
                if (assignedClassCourseIds.Contains(classCourse.ClassCourseId))
                    continue;
                // Haftalık ders saati ve formatını al
                int weeklyHours = classCourse.Course.WeeklyHourCount;
                string placementFormat = classCourse.Course.PlacementFormat?.Trim() ?? string.Empty;
                
                if (string.IsNullOrEmpty(placementFormat))
                {
                    // Format belirtilmemişse tüm saatleri tek blok olarak ekle
                    var block = new CourseBlock
                    {
                        ClassCourse = classCourse,
                        BlockSize = weeklyHours  // Tüm saatleri tek blok olarak ayarla
                    };
                    
                    lessonBlocks.Add(block);
                }
                else
                {
                    // Format örneği: "3,2" veya "3" 
                    var blockSizes = placementFormat.Split(',').Select(s => int.TryParse(s.Trim(), out int size) ? size : 0).Where(s => s > 0).ToList();
                    
                    // Format geçerli mi kontrol et
                    int totalHours = blockSizes.Sum();
                    if (totalHours != weeklyHours)
                    {
                        // Format geçerli değilse, tüm saatleri tek blok olarak ekle
                        var block = new CourseBlock
                        {
                            ClassCourse = classCourse,
                            BlockSize = weeklyHours
                        };
                        
                        lessonBlocks.Add(block);
                    }
                    else
                    {
                        // Format geçerliyse, her bir blok büyüklüğü için ayrı bir CourseBlock oluştur
                        foreach (int blockSize in blockSizes)
                        {
                            var block = new CourseBlock
                            {
                                ClassCourse = classCourse,
                                BlockSize = blockSize
                            };
                            
                            lessonBlocks.Add(block);
                        }
                    }
                }
            }
            
            return lessonBlocks;
        }

        

    }
}

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


// Ders bloğu sınıfı
public class CourseBlock
{
    public ClassCourse ClassCourse { get; set; }
    public int BlockSize { get; set; } // Ders bloğunun büyüklüğü/süresi
}

public class Wolf
{
    public List<PlacementRepresentation> Placements { get; set; } = new List<PlacementRepresentation>();
    public double Fitness { get; set; }
    
    public Wolf Clone()
    {
        return new Wolf
        {
            Placements = this.Placements.Select(p => p.Clone()).ToList(),
            Fitness = this.Fitness
        };
    }
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