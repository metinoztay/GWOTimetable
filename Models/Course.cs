using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Course
{
    public int CourseId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public byte WeeklyHourCount { get; set; }

    public string PlacementFormat { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();

    public virtual Workspace Workspace { get; set; } = null!;
}
