using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.RegularExpressions;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Reflection.Metadata;


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

            List<Day> days = new List<Day>();
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Pazartesi", LessonCount = 8, ShortName = "Pzt" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Salı", LessonCount = 8, ShortName = "Sal" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Çarşamba", LessonCount = 8, ShortName = "Çar" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Perşembe", LessonCount = 8, ShortName = "Per" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Cuma", LessonCount = 8, ShortName = "Cum" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Cumartesi", LessonCount = 8, ShortName = "Cmt" });
            days.Add(new Day() { WorkspaceId = workspace.WorkspaceId, DayOfWeek = "Pazar", LessonCount = 8, ShortName = "Paz" });

            await _context.Days.AddRangeAsync(days);
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

            var identity = (ClaimsIdentity)User.Identity;
            var emailClaim = identity.FindFirst("Email");
            if (emailClaim != null)
            {
                identity.RemoveClaim(emailClaim);
            }
            identity.AddClaim(new Claim("Email", user.Email));

            var firstnameClaim = identity.FindFirst("FirstName");
            if (firstnameClaim != null)
            {
                identity.RemoveClaim(firstnameClaim);
            }
            identity.AddClaim(new Claim("FirstName", user.FirstName));

            var lastnameClaim = identity.FindFirst("LastName");
            if (lastnameClaim != null)
            {
                identity.RemoveClaim(lastnameClaim);
            }
            identity.AddClaim(new Claim("LastName", user.LastName));

            var newPrincipal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                newPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true
                }
            );


            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> SendVerificationCode()
        {
            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user.IsVerified)
            {
                return BadRequest(new { message = "User is already verified!" });
            }

            VerificationCode verification = _context.VerificationCodes.FirstOrDefault(v => v.UserId == user.UserId);
            if (verification != null && verification.ExpirationAt > DateTime.Now)
            {
                string waitTime = (verification.ExpirationAt - DateTime.Now).ToString(@"mm\:ss");
                return BadRequest(new { message = "Verification code has been sent. The time you have to wait before you can send the code again: \n\n" + waitTime });
            }

            if (verification != null)
            {
                _context.VerificationCodes.Remove(verification);
                await _context.SaveChangesAsync();
            }

            string verificationCode = Utilities.GenerateVerificationCode(6);
            string verificationCodeHash = Utilities.CreateHash("123456");

            VerificationCode newCode = new VerificationCode()
            {
                UserId = user.UserId,
                CodeHash = verificationCodeHash,
                CreatedAt = DateTime.Now,
                ExpirationAt = DateTime.Now.AddMinutes(5)
            };
            _context.VerificationCodes.Add(newCode);
            await _context.SaveChangesAsync();
            //burada mail gönderilecek

            return Ok(new { RedirectUrl = $"/Account/Verify" });
        }

        public IActionResult Verify()
        {
            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user.IsVerified)
            {
                return RedirectToAction("Dashboard", "Home");
            }

            VerificationCode verification = _context.VerificationCodes.FirstOrDefault(v => v.UserId == user.UserId);
            if (verification == null)
            {
                return RedirectToAction("Profile", "Account");
            }

            if (verification.ExpirationAt < DateTime.Now)
            {
                return RedirectToAction("Profile", "Account");
            }

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> ComfirmVerificationCode([FromBody] VerificationCodeDTO verificationCode)
        {
            if (string.IsNullOrEmpty(verificationCode.Code))
            {
                return BadRequest(new { message = "Verification code cannot be empty!" });
            }

            if (verificationCode.Code.Length != 6)
            {
                return BadRequest(new { message = "Verification code must be 6 characters!" });
            }

            if (!Utilities.IsNumeric(verificationCode.Code))
            {
                return BadRequest(new { message = "Verification code must be numeric!" });
            }

            Guid? userId = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userId == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user.IsVerified)
            {
                return BadRequest(new { message = "User is already verified!" });
            }

            VerificationCode verification = _context.VerificationCodes.FirstOrDefault(v => v.UserId == user.UserId);
            if (verification == null)
            {
                return BadRequest(new { message = "Verification code not found!" });
            }

            if (verification.ExpirationAt < DateTime.Now)
            {
                return BadRequest(new { message = "Verification code has expired!" });
            }

            if (verification.CodeHash != Utilities.CreateHash(verificationCode.Code))
            {
                return BadRequest(new { message = "Verification code is incorrect!" });
            }

            user.IsVerified = true;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Verification successful!" });
        }

        private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/UserProfilePhotos");
        [HttpPost]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            Guid? userGuid = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userGuid == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userGuid);

            if (user == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            string userId = user.UserId.ToString();

            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Lütfen bir dosya yükleyin." });

            var validImageTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!validImageTypes.Contains(file.ContentType))
                return BadRequest(new { message = "Please select only a valid photo file (JPEG, PNG, JPG)." });

            if (file.Length > 2 * 1024 * 1024)
            {
                return BadRequest(new { message = "Please upload a file smaller than 2MB." });
            }


            var fileName = Path.GetFileName($"user_{userId}{Path.GetExtension(file.FileName)}");
            var filePath = Path.Combine(_uploadPath, fileName);

            using (var image = Image.Load(file.OpenReadStream()))
            {

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Stretch,
                    Size = new Size(400, 400)
                }));


                var encoder = new JpegEncoder
                {
                    Quality = 75
                };

                await image.SaveAsync(filePath, encoder);
            }


            user.PhotoUrl = $"{Request.Scheme}://{Request.Host}//images//UserProfilePhotos//{fileName}";
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();


            var identity = (ClaimsIdentity)User.Identity;
            var photoClaim = identity.FindFirst("PhotoUrl");
            if (photoClaim != null)
            {
                identity.RemoveClaim(photoClaim);
            }
            identity.AddClaim(new Claim("PhotoUrl", user.PhotoUrl));

            var newPrincipal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                newPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true
                }
            );

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPhoto()
        {

            Guid? userGuid = User.FindFirstValue("UserId") != null ? Guid.Parse(User.FindFirstValue("UserId")) : (Guid?)null;
            if (userGuid == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            User user = _context.Users.FirstOrDefault(u => u.UserId == userGuid);
            if (user == null)
            {
                return BadRequest(new { message = "User not found!" });
            }
            user.PhotoUrl = $"{Request.Scheme}://{Request.Host}/ThemeData/defaultAvatar.jpg";
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var identity = (ClaimsIdentity)User.Identity;
            var photoClaim = identity.FindFirst("PhotoUrl");
            if (photoClaim != null)
            {
                identity.RemoveClaim(photoClaim);
            }
            identity.AddClaim(new Claim("PhotoUrl", user.PhotoUrl));

            var newPrincipal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                newPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true
                }
            );

            return Ok();
        }

    }
}


