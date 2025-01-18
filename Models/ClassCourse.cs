using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class ClassCourse
{
    public int ClassCourseId { get; set; }

    public Guid WorkspaceId { get; set; }

    public int ClassId { get; set; }

    public int CourseId { get; set; }

    public int ClassRoomId { get; set; }

    public int EducatorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Classroom ClassRoom { get; set; } = null!;

    public virtual Course Course { get; set; } = null!;

    public virtual Educator Educator { get; set; } = null!;

    public virtual ICollection<TimetableConstraint> TimetableConstraints { get; set; } = new List<TimetableConstraint>();

    public virtual Workspace Workspace { get; set; } = null!;
}
