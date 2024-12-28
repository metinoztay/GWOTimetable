using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class TimetableState
{
    public int TimetableStateId { get; set; }

    public string State { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();
}
