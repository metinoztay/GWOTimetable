using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GWOTimetable.DTOs
{
    public class SaveClassConstraintsRequestDto
    {
        [Required]
        public int ClassId { get; set; }

        public List<ClassConstraintDto> ConstraintsToAdd { get; set; } = new List<ClassConstraintDto>();
        
        public List<ClassConstraintDto> ConstraintsToRemove { get; set; } = new List<ClassConstraintDto>();
    }
}
