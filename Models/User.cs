using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int RoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsVerified { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<VerificationCode> VerificationCodes { get; set; } = new List<VerificationCode>();

    public virtual ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}
