using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Educator
{
    public int EducatorId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? Tittle { get; set; }

    public string ShortName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();

    public virtual ICollection<EducatorConstraint> EducatorConstraints { get; set; } = new List<EducatorConstraint>();

    public virtual Workspace Workspace { get; set; } = null!;
}
