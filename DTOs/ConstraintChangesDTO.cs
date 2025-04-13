using System.Collections.Generic;

namespace GWOTimetable.Models
{
    public class ConstraintChangesDTO
    {
        public List<ConstraintDTO> ConstraintsToAdd { get; set; } = new List<ConstraintDTO>();
        public List<ConstraintDTO> ConstraintsToRemove { get; set; } = new List<ConstraintDTO>();
    }
}
