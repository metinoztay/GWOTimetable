using System;
using System.Collections.Generic;

namespace GWOTimetable.Models;

public partial class VerificationCode
{
    public int VerificationCodeId { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpirationAt { get; set; }

    public virtual User User { get; set; } = null!;
}
