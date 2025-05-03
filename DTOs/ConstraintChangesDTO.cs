using System.Collections.Generic;

namespace GWOTimetable.Models
{
    public class ConstraintChangesDTO
    {
        public List<ConstraintDTO> ConstraintsToAdd { get; set; } = new List<ConstraintDTO>();
        public List<ConstraintDTO> ConstraintsToRemove { get; set; } = new List<ConstraintDTO>();
        public int CourseId { get; set; } // Kurs kısıtlamaları için eklendi
        public int ClassId { get; set; } // Sınıf kısıtlamaları için zaten vardı
    }
}
