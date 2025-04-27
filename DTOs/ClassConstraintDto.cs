using System.ComponentModel.DataAnnotations;

namespace GWOTimetable.DTOs
{
    public class ClassConstraintDto
    {
        public int DayId { get; set; }
        
        public int LessonId { get; set; }
        
        // ClassId, SaveClassConstraintsRequestDto üzerinden geldiği için burada zorunlu değil
        public int ClassId { get; set; }
        
        // Ders (course) tipi kısıtlamaları için gerekli
        public int ClassCourseId { get; set; }
        
        // Eğitmen kısıtlamaları için gerekli
        public int EducatorId { get; set; }
        
        // Derslik kısıtlamaları için gerekli
        public int ClassroomId { get; set; }
    }
}
