using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Classroom
{
    public int ClassroomId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string ClassroomName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();

    public virtual Workspace Workspace { get; set; } = null!;
}
