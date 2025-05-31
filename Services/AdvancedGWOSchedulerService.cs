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
        private const int MAX_ITERATIONS = 500; // Daha fazla iterasyon ile daha iyi çözümler
        private const int POPULATION_SIZE = 200; // Number of wolves in the packy

        // GWO parametersy
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
                //timetable.TimetableStateId = 2;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                // Tüm kısıtlamaları ve gerekli verileri yükle
                var workspaceId = timetable.WorkspaceId;
                var data = await LoadRequiredDataAsync(workspaceId);

                // GWO algoritmasını çalıştır
                var placements = RunGWOAlgorithm(data, timetableId);
/*
                // Sonuçları kaydet
                await SavePlacementsAsync(placements, timetable);

                // Zaman tablosu durumunu "Completed" (varsayılan olarak stateId 3) olarak güncelle
                timetable.TimetableStateId = 3;
                timetable.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
*/
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

        private List<CourseBlock> RunGWOAlgorithm(SchedulingData data, int timetableId)
        {
            var lessonBlocks = CreateSortedLessonBlocks(data);

            var wolves = InitializeWolfPopulation(lessonBlocks, data, POPULATION_SIZE);
            return lessonBlocks;
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