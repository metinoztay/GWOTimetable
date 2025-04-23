using System.ComponentModel.DataAnnotations;

namespace GWOTimetable.DTOs
{
    public class ClearClassConstraintsRequestDto
    {
        [Required]
        public int ClassId { get; set; }
    }
}
