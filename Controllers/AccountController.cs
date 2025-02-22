using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.RegularExpressions;


namespace GWOTimetable.Controllers
{
    public class AccountController : Controller
    {
        private readonly Db12026Context _context;

        public AccountController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] User user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { message = "Email can not be empty!" });
            }
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(user.Email.Trim(), emailRegex))
            {
                return BadRequest(new { message = "Invalid email format!" });
            }
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return BadRequest(new { message = "Password cannot be empty!" });
            }


            string hashPassword = Utilities.CreateHash(user.PasswordHash);
            var userInformations = _context.Users.Include(u => u.Role).Where(u => u.Email == user.Email && u.PasswordHash == hashPassword).FirstOrDefault();
            var workspace = _context.Workspaces.Where(u => u.UserId == userInformations.UserId).OrderBy(u => u.CreatedAt).ToList();

            if (userInformations != null) //kullanıcı bulundu
            {
                var claims = new List<Claim>()
                {
                    new Claim("Email",userInformations.Email),
                    new Claim("FirstName",userInformations.FirstName),
                    new Claim("LastName",userInformations.LastName),
                    new Claim(ClaimTypes.Role,userInformations.Role.RoleName),
                    new Claim("UserId",userInformations.UserId.ToString()),
                    new Claim("PhotoUrl",userInformations.PhotoUrl),
                    new Claim("WorkspaceId",workspace[0].WorkspaceId.ToString()),
                    new Claim("WorkspaceName",workspace[0].WorkspaceName)
                };
                var userIdentity = new ClaimsIdentity(claims, "Login"); //kullanıcı kimliği oluşturuldu

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(userIdentity),
                    new AuthenticationProperties
                    {
                        IsPersistent = true, // Kalıcı oturum açma
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) // Oturum süresi
                    });

                return Ok(new
                {
                    redirectUrl = Url.Action("Dashboard", "Home"),
                    message = "Login success!"
                });
            }

            return BadRequest(new { message = "Invalid email or password!" });
        }


        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup([FromBody] User user, [FromQuery] bool isTermConfirmed)
        {
            if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
            {
                return BadRequest(new { message = "First Name and Last Name cannot be empty!" });
            }
            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(user.FirstName.Trim(), nameRegex) || !Regex.IsMatch(user.LastName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "First Name and Last Name must contain only letters!" });
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { message = "Email can not be empty!" });
            }
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(user.Email.Trim(), emailRegex))
            {
                return BadRequest(new { message = "Invalid email format!" });
            }
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return BadRequest(new { message = "Password cannot be empty!" });
            }

            if (_context.Users.Any(u => u.Email == user.Email.Trim()))
            {
                return BadRequest(new { message = "Email is already in use!" });
            }

            if (!isTermConfirmed)
            {
                return BadRequest(new { message = "Please read and accept terms and policy!" });
            }

            string hashPassword = Utilities.CreateHash(user.PasswordHash);
            user.PasswordHash = hashPassword;

            user.RoleId = 1;
            user.IsVerified = false;
            user.PhotoUrl = $"{Request.Scheme}://{Request.Host}/ThemeData/defaultAvatar.jpg";
            user.LastName = Utilities.ToProperCase(user.LastName.Trim());
            user.FirstName = Utilities.ToProperCase(user.FirstName.Trim());
            user.Email = user.Email.Trim().ToLower();
            user.CreatedAt = DateTime.Now;

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            Workspace workspace = new Workspace();
            workspace.UserId = user.UserId;
            workspace.WorkspaceName = "Default Workspace";
            workspace.Description = "Default Workspace for " + user.FirstName + " " + user.LastName;
            workspace.CreatedAt = DateTime.Now;
            await _context.Workspaces.AddAsync(workspace);
            await _context.SaveChangesAsync();

            return Ok(new { RedirectUrl = $"/Account/Login" });
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> SendPassword([FromBody] User user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { message = "Email can not be empty!" });
            }
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(user.Email.Trim(), emailRegex))
            {
                return BadRequest(new { message = "Invalid email format!" });
            }

            if (!_context.Users.Any(u => u.Email == user.Email.Trim()))
            {
                return BadRequest(new { message = "Email cannot be found!" });
            }

            User userInformations = _context.Users.Where(u => u.Email == user.Email.Trim()).FirstOrDefault();

            if (userInformations.UpdatedAt != null)
            {
                if (userInformations.UpdatedAt.Value.AddMinutes(5) > DateTime.Now)
                {
                    return BadRequest(new { message = "You can not send password again in 5 minutes!" });
                }
            }

            string tempPwd = Utilities.GeneratePassword(8);
            string hashPassword = Utilities.CreateHash(tempPwd);
            userInformations.PasswordHash = hashPassword;
            userInformations.UpdatedAt = DateTime.Now;

            _context.Entry(userInformations).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            //burada temp pwd email gönderim yapılacak

            return Ok(new { RedirectUrl = $"/Account/Login" });
        }

        [HttpPut]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDTO newPassword)
        {
            if (string.IsNullOrEmpty(newPassword.OldPassword))
            {
                return BadRequest(new { message = "Old Password can not be empty!" });
            }

            if (string.IsNullOrEmpty(newPassword.NewPassword))
            {
                return BadRequest(new { message = "New Password cannot be empty!" });
            }

            if (string.IsNullOrEmpty(newPassword.ComfirmNewPassword))
            {
                return BadRequest(new { message = "Please Comfirm New Password!" });
            }

            string oldPasswordHash = Utilities.CreateHash(newPassword.OldPassword);

            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return BadRequest(new { message = "User not found!" });
            }

            User user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user.PasswordHash != oldPasswordHash)
            {
                return BadRequest(new { message = "Old Password is incorrect!" });
            }

            if (newPassword.NewPassword != newPassword.ComfirmNewPassword)
            {
                return BadRequest(new { message = "New Passwords do not match!" });
            }

            string newPasswordHash = Utilities.CreateHash(newPassword.NewPassword);

            user.PasswordHash = newPasswordHash;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }



        public IActionResult Profile()
        {
            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            User user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == userId);

            ViewBag.ActiveTabId = "Profile";
            return View(user);
        }

        public async Task<IActionResult> UpdateProfile([FromBody] User newProfile)
        {
            if (string.IsNullOrWhiteSpace(newProfile.PasswordHash))
            {
                return BadRequest(new { message = "Password cannot be empty!" });
            }

            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            string hashPassword = Utilities.CreateHash(newProfile.PasswordHash);
            if (user.PasswordHash != hashPassword)
            {
                return BadRequest(new { message = "Password is incorrect!" });
            }

            if (string.IsNullOrWhiteSpace(newProfile.FirstName) || string.IsNullOrWhiteSpace(newProfile.LastName))
            {
                return BadRequest(new { message = "First Name and Last Name cannot be empty!" });
            }
            var nameRegex = @"^[a-zA-Z\sçÇğĞıİöÖşŞüÜ]+$";
            if (!Regex.IsMatch(newProfile.FirstName.Trim(), nameRegex) || !Regex.IsMatch(newProfile.LastName.Trim(), nameRegex))
            {
                return BadRequest(new { message = "First Name and Last Name must contain only letters!" });
            }

            if (string.IsNullOrWhiteSpace(newProfile.Email))
            {
                return BadRequest(new { message = "Email can not be empty!" });
            }
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(newProfile.Email.Trim(), emailRegex))
            {
                return BadRequest(new { message = "Invalid email format!" });
            }



            if (_context.Users.Any(u => u.Email == newProfile.Email.Trim() && u.UserId != user.UserId))
            {
                return BadRequest(new { message = "Email is already in use!" });
            }




            user.PasswordHash = hashPassword;
            if (user.Email != newProfile.Email.Trim())
            {
                user.IsVerified = false;
            }

            user.LastName = Utilities.ToProperCase(newProfile.LastName.Trim());
            user.FirstName = Utilities.ToProperCase(newProfile.FirstName.Trim());
            user.Email = newProfile.Email.Trim().ToLower();
            user.UpdatedAt = DateTime.Now;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

    }
}


