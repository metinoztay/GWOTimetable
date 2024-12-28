using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class EducatorConstraint
{
    public int EducatorConstraints { get; set; }

    public Guid WorkspaceId { get; set; }

    public int EducatorId { get; set; }

    public int DayId { get; set; }

    public int LessonId { get; set; }

    public bool IsPlaceable { get; set; }

    public virtual Day Day { get; set; } = null!;

    public virtual Educator Educator { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual Workspace Workspace { get; set; } = null!;
}
