using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class ClassroomConstraint
{
    public int ClassroomConstraintId { get; set; }

    public Guid WorkspaceId { get; set; }

    public int ClassroomId { get; set; }

    public int DayId { get; set; }

    public int LessonId { get; set; }

    public bool IsPlaceable { get; set; }
}
