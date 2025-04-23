using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class ClassConstraint
{
    public int ClassConstraintId { get; set; }

    public Guid WorkspaceId { get; set; }

    public int ClassId { get; set; }

    public int DayId { get; set; }

    public int LessonId { get; set; }

    public bool IsPlaceable { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Day Day { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual Workspace Workspace { get; set; } = null!;
}
