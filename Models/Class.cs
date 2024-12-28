using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Class
{
    public int ClassId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string ClassName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassConstraint> ClassConstraints { get; set; } = new List<ClassConstraint>();

    public virtual ICollection<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();

    public virtual Workspace Workspace { get; set; } = null!;
}
