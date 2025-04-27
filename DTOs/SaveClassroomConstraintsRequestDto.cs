using System.ComponentModel.DataAnnotations;

namespace GWOTimetable.DTOs
{
    public class SaveClassroomConstraintsRequestDto
    {
        [Required]
        public int ClassroomId { get; set; }
        
        public List<ClassConstraintDto> ConstraintsToAdd { get; set; } = new List<ClassConstraintDto>();
        
        public List<ClassConstraintDto> ConstraintsToRemove { get; set; } = new List<ClassConstraintDto>();
    }
}
