
namespace GWOTimetable.Models;

public class UpdatePasswordDTO
{
    public string OldPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
    public string ComfirmNewPassword { get; set; } = null!;
}
