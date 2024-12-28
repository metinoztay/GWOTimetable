using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class Workspace
{
    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    public string WorkspaceName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassConstraint> ClassConstraints { get; set; } = new List<ClassConstraint>();

    public virtual ICollection<ClassCourse> ClassCourses { get; set; } = new List<ClassCourse>();

    public virtual ICollection<ClassRoom> ClassRooms { get; set; } = new List<ClassRoom>();

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual ICollection<Day> Days { get; set; } = new List<Day>();

    public virtual ICollection<EducatorConstraint> EducatorConstraints { get; set; } = new List<EducatorConstraint>();

    public virtual ICollection<Educator> Educators { get; set; } = new List<Educator>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<TimetableConstraint> TimetableConstraints { get; set; } = new List<TimetableConstraint>();

    public virtual ICollection<TimetablePlacement> TimetablePlacements { get; set; } = new List<TimetablePlacement>();

    public virtual ICollection<Timetable> Timetables { get; set; } = new List<Timetable>();

    public virtual User User { get; set; } = null!;
}
