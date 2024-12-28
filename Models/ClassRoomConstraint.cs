using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class ClassRoomConstraint
{
    public int ClassRoomConstraintId { get; set; }

    public Guid WorkspaceId { get; set; }

    public int ClassRoomId { get; set; }

    public int DayId { get; set; }

    public int LessonId { get; set; }

    public bool IsPlaceable { get; set; }
}
