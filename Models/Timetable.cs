using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Timetable
{
    public int TimetableId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Tag { get; set; } = null!;

    public string? Description { get; set; }

    public int TimetableStateId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<TimetablePlacement> TimetablePlacements { get; set; } = new List<TimetablePlacement>();

    public virtual TimetableState TimetableState { get; set; } = null!;

    public virtual Workspace Workspace { get; set; } = null!;
}
