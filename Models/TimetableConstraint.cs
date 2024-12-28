using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class TimetableConstraint
{
    public int TimetableConstraintId { get; set; }

    public Guid WorkspaceId { get; set; }

    public int DayId { get; set; }

    public int LessonId { get; set; }

    public int ClassCourseId { get; set; }

    public virtual ClassCourse ClassCourse { get; set; } = null!;

    public virtual Day Day { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual Workspace Workspace { get; set; } = null!;
}
