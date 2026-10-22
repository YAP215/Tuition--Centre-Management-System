using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Security.Claims;
using TutionCenter.Data;
using TutionCenter.Models;
using TutionCenter.Services;

namespace TutionCenter.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public AccountController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // 显示 Register 页面
        public IActionResult Register()
        {
            ViewBag.CanCreateFirstAdmin = !_context.Users.Any(x => x.Role == "Admin");
            return View();
        }

        public IActionResult Login()
        {
            GenerateCaptcha();
            return View();
        }

        public IActionResult VerifyEmail(string token)
        {
            var user = _context.Users.FirstOrDefault(x => x.EmailVerificationToken == token);

            if (user == null || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            {
                return Content("This verification link is invalid or has expired.");
            }

            user.IsVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;
            _context.SaveChanges();

            return Content("Email verified successfully! You can now log in.");
        }

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult TestEmail()
        {
            try
            {
                _emailService.SendEmail(
                    User.FindFirstValue(ClaimTypes.Email)!,
                    "Test Email",
                    "<h2>Hello!</h2><p>This is a test email from Tuition Center System.</p>");
                return Content("Email sent successfully!");
            }
            catch
            {
                return Content("Email could not be sent. Please check EmailSettings in appsettings.json.");
            }
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        public IActionResult ResetPassword(string token)
        {
            var user = _context.Users.FirstOrDefault(x => x.ResetToken == token);

            if (user == null || user.ResetTokenExpiresAt < DateTime.UtcNow)
            {
                return Content("Invalid or expired reset link.");
            }

            var model = new ResetPasswordViewModel
            {
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }

        private void GenerateCaptcha()
        {
            Random random = new Random();

            int num1 = random.Next(1, 10);
            int num2 = random.Next(1, 10);

            HttpContext.Session.SetInt32("Captcha", num1 + num2);

            ViewBag.Num1 = num1;
            ViewBag.Num2 = num2;
        }

        // 处理 Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(User user)
        {
            // Only the first account can become Admin. After that, public
            // registration always creates a Student account.
            bool canCreateFirstAdmin = !_context.Users.Any(x => x.Role == "Admin");
            if (!canCreateFirstAdmin || user.Role != "Admin")
                user.Role = "Student";

            if (ModelState.IsValid)
            {
                // Check duplicate email
                if (_context.Users.Any(x => x.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    ViewBag.CanCreateFirstAdmin = canCreateFirstAdmin;
                    return View(user);
                }

                user.IsVerified = false;
                user.Password = PasswordHelper.Hash(user.Password);
                user.EmailVerificationToken = CreateSecureToken();
                user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);

                _context.Users.Add(user);
                _context.SaveChanges();

                string link = Url.Action(
                    "VerifyEmail",
                    "Account",
                    new { token = user.EmailVerificationToken },
                    Request.Scheme);

                try
                {
                    _emailService.SendEmail(
                        user.Email,
                        "Verify Your Email",
                        $"<h2>Welcome to Tuition Center!</h2>" +
                        $"<p>Please click the link below to verify your account:</p>" +
                        $"<a href='{link}'>Verify Email</a>");
                    TempData["Success"] = "Registration successful! Please check your email to verify your account.";
                }
                catch
                {
                    // Keeps local demonstrations usable when a Gmail App Password is not configured.
                    TempData["Success"] = "Registration successful. Email is not configured, so use the test verification link below.";
                    TempData["VerificationLink"] = link;
                }

                return RedirectToAction("Login", "Account");
            }

            ViewBag.CanCreateFirstAdmin = canCreateFirstAdmin;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel user)
        {

            if (!ModelState.IsValid)
            {
                GenerateCaptcha();
                return View(user);
            }

            int? answer = HttpContext.Session.GetInt32("Captcha");

            if (answer == null || user.CaptchaAnswer != answer)
            {
                ViewBag.Message = "Incorrect Captcha.";

                GenerateCaptcha();

                return View(user);
            }

            // 1. 根据 Email 找帐号
            var account = _context.Users.FirstOrDefault(x => x.Email == user.Email);

            if (account == null)
            {
                ViewBag.Message = "Invalid Email or Password.";

                GenerateCaptcha();

                return View(user);
            }

            // 2. 检查是否被锁定
            if (account.LockoutEnd.HasValue &&
                account.LockoutEnd > DateTime.Now)
            {
                ViewBag.Message = $"Your account is locked until {account.LockoutEnd:HH:mm:ss}";

                GenerateCaptcha();

                return View(user);
            }

            // 3. 检查密码
            bool validPassword = PasswordHelper.Verify(user.Password, account.Password);

            // Allows the supplied sample accounts to work once, then upgrades them.
            if (!validPassword && account.Password == user.Password)
            {
                account.Password = PasswordHelper.Hash(user.Password);
                validPassword = true;
            }

            if (!validPassword)
            {
                account.FailedLoginAttempts++;

                if (account.FailedLoginAttempts >= 3)
                {
                    account.LockoutEnd = DateTime.Now.AddMinutes(5);
                    account.FailedLoginAttempts = 0;

                    ViewBag.Message = "Your account has been locked for 5 minutes because you entered the wrong password 3 times.";
                }
                else
                {
                    ViewBag.Message = $"Invalid password. Remaining attempts: {3 - account.FailedLoginAttempts}";
                }

                _context.SaveChanges();

                GenerateCaptcha();

                return View(user);
            }

            // 4. 检查 Email 是否已验证
            if (!account.IsVerified)
            {
                ViewBag.Message = "Please verify your email before logging in.";

                GenerateCaptcha();

                return View(user);
            }

            // 5. 登录成功，清除错误次数和锁定时间
            account.FailedLoginAttempts = 0;
            account.LockoutEnd = null;
            _context.SaveChanges();
            HttpContext.Session.Remove("Captcha");

            // 6. 建立 Claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier,account.Id.ToString()),
        new Claim(ClaimTypes.Name, account.Name),
        new Claim(ClaimTypes.Email, account.Email),
        new Claim(ClaimTypes.Role, account.Role)
    };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email = User.FindFirstValue(ClaimTypes.Email);

            var user = _context.Users.FirstOrDefault(x => x.Email == email);

            if (user == null)
                return RedirectToAction("Login");

            if (!PasswordHelper.Verify(model.CurrentPassword, user.Password))
            {
                ViewBag.Message = "Current password is incorrect.";
                return View(model);
            }

            user.Password = PasswordHelper.Hash(model.NewPassword);

            _context.SaveChanges();

            ViewBag.Message = "Password changed successfully.";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(x => x.Email == model.Email);

            // Show the same message for an unknown email, so an attacker cannot
            // use this page to discover registered accounts.
            if (user == null)
            {
                ViewBag.Message = "If this email is registered, a reset link has been sent.";
                return View();
            }

            // 产生随机 Token
            user.ResetToken = CreateSecureToken();
            user.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);

            _context.SaveChanges();

            string link = Url.Action(
                "ResetPassword",
                "Account",
                new { token = user.ResetToken },
                Request.Scheme);

            try
            {
                _emailService.SendEmail(
                user.Email,
                "Reset Password",
                $@"
                <h2>Password Reset</h2>
                <p>Click the link below to reset your password:</p>
                <a href=""{link}"">{link}</a>");
                ViewBag.Message = "A password reset link has been sent. Please check your email.";
            }
            catch
            {
                ViewBag.Message = "Email is not configured. Use the test reset link below.";
                ViewBag.ResetLink = link;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(x => x.ResetToken == model.Token);

            if (user == null || user.ResetTokenExpiresAt < DateTime.UtcNow)
            {
                ViewBag.Message = "This reset link is invalid or has expired.";
                return View(model);
            }

            user.Password = PasswordHelper.Hash(model.NewPassword);

            // 用过一次后清除 Token
            user.ResetToken = null;
            user.ResetTokenExpiresAt = null;

            _context.SaveChanges();

            TempData["Success"] = "Password reset successfully.";

            return RedirectToAction("Login");
        }

        private static string CreateSecureToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

    }
}
