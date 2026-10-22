using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers
{
    // 所有已经登录的用户都可以使用 Profile
    [Authorize]
    public class ProfileController : Controller
    {
        // 数据库连接
        private readonly ApplicationDbContext _context;

        // 使用 Dependency Injection 注入数据库
        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 显示目前登录用户的 Profile
        public IActionResult Index()
        {
            // 从登录 Claims 取得目前用户的 Email
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);

            // 如果无法取得 Email，就返回 Login 页面
            if (string.IsNullOrEmpty(currentEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // 根据 Email 查询当前登录用户
            var user = _context.Users
                .FirstOrDefault(x => x.Email == currentEmail);

            // 如果找不到用户，返回 404
            if (user == null)
            {
                return NotFound();
            }

            // 把 User 资料转换成 ProfileViewModel
            var model = new ProfileViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                ProfileImage = user.ProfileImage
            };

            // 显示 Views/Profile/Index.cshtml
            return View(model);
        }

        // 显示 Edit Profile 页面
        [HttpGet]
        public IActionResult Edit()
        {
            // 从登录 Claims 取得目前用户的 Email
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);

            // 如果无法取得 Email，就返回 Login 页面
            if (string.IsNullOrEmpty(currentEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // 查询当前登录用户
            var user = _context.Users
                .FirstOrDefault(x => x.Email == currentEmail);

            // 如果找不到用户，返回 404
            if (user == null)
            {
                return NotFound();
            }

            // 把 User 资料放进 ViewModel
            var model = new ProfileViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                ProfileImage = user.ProfileImage
            };

            // 显示 Views/Profile/Edit.cshtml
            return View(model);
        }

        // 接收 Edit Profile 表单提交
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            ProfileViewModel model,
            IFormFile? profilePhoto)
        {
            // Email 和 Role 不允许用户自己修改
            ModelState.Remove("Email");
            ModelState.Remove("Role");

            // 从登录 Claims 取得目前用户 Email
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);

            // 如果没有登录资料，返回 Login 页面
            if (string.IsNullOrEmpty(currentEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // 根据 Email 查询当前用户
            var user = _context.Users
                .FirstOrDefault(x => x.Email == currentEmail);

            // 如果找不到用户，返回 404
            if (user == null)
            {
                return NotFound();
            }

            // 检查上传文件
            if (profilePhoto != null && profilePhoto.Length > 0)
            {
                // 允许的图片扩展名
                var allowedExtensions = new[]
                {
                    ".jpg",
                    ".jpeg",
                    ".png"
                };

                // 取得上传文件扩展名
                var extension = Path
                    .GetExtension(profilePhoto.FileName)
                    .ToLowerInvariant();

                // 检查文件格式
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        "ProfileImage",
                        "Only JPG, JPEG and PNG files are allowed.");
                }

                // 限制图片最大为 2MB
                if (profilePhoto.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "ProfileImage",
                        "Profile image must be smaller than 2MB.");
                }
            }

            // 如果资料验证失败
            if (!ModelState.IsValid)
            {
                // 补回不能由用户修改的资料
                model.Email = user.Email;
                model.Role = user.Role;
                model.ProfileImage = user.ProfileImage;

                return View(model);
            }

            // 只更新允许用户修改的资料
            user.Name = model.Name;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;

            // 如果有上传新头像
            if (profilePhoto != null && profilePhoto.Length > 0)
            {
                // 建立 wwwroot/images/profile 文件夹路径
                var folder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "profile");

                // 如果资料夹不存在就建立
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 取得图片扩展名
                var extension = Path
                    .GetExtension(profilePhoto.FileName)
                    .ToLowerInvariant();

                // 使用 Guid 产生不会重复的文件名
                var fileName = Guid.NewGuid().ToString() + extension;

                // 完整储存路径
                var filePath = Path.Combine(folder, fileName);

                // 储存图片
                using (var stream = new FileStream(
                    filePath,
                    FileMode.Create))
                {
                    profilePhoto.CopyTo(stream);
                }

                // 如果之前已经有头像，可以删除旧头像
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldFileName = Path.GetFileName(user.ProfileImage);

                    var oldFilePath = Path.Combine(
                        folder,
                        oldFileName);

                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // 把图片相对路径存入数据库
                user.ProfileImage =
                    "/images/profile/" + fileName;
            }

            // 保存资料到数据库
            _context.SaveChanges();

            // 显示一次性的成功信息
            TempData["SuccessMessage"] =
                "Profile updated successfully.";

            // 返回 Profile 页面
            return RedirectToAction("Index");
        }
    }
}