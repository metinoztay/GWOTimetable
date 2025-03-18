using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Lesson
{
    public int LessonId { get; set; }

    public Guid WorkspaceId { get; set; }

    public byte LessonNumber { get; set; }

    public string StartTime { get; set; } = null!;

    public string EndTime { get; set; } = null!;

    public virtual ICollection<ClassConstraint> ClassConstraints { get; set; } = new List<ClassConstraint>();

    public virtual ICollection<ClassroomConstraint> ClassroomConstraints { get; set; } = new List<ClassroomConstraint>();

    public virtual ICollection<EducatorConstraint> EducatorConstraints { get; set; } = new List<EducatorConstraint>();

    public virtual ICollection<TimetableConstraint> TimetableConstraints { get; set; } = new List<TimetableConstraint>();

    public virtual Workspace Workspace { get; set; } = null!;
}
