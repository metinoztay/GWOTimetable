using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class TimetablePlacement
{
    public int TimetablePlacementId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public byte LessonNumber { get; set; }

    public string StartTime { get; set; } = null!;

    public string EndTime { get; set; } = null!;

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public string ClassroomName { get; set; } = null!;

    public string ClassName { get; set; } = null!;

    public string EducatorFullName { get; set; } = null!;

    public string EducatorShortName { get; set; } = null!;

    public virtual Workspace Workspace { get; set; } = null!;
}
