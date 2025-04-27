using System.Collections.Generic;
using GWOTimetable.Models;

namespace GWOTimetable.DTOs
{
    public class ClassroomConstraintDTO
    {
        public int ClassroomId { get; set; }
        
        public List<ConstraintDTO> ConstraintsToAdd { get; set; } = new List<ConstraintDTO>();
        
        public List<ConstraintDTO> ConstraintsToRemove { get; set; } = new List<ConstraintDTO>();
    }
}
