using blog_DACS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using blog_DACS.View_Models;
using Microsoft.CodeAnalysis.Scripting;
using BCrypt.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Net;
namespace blog_DACS.Controllers
{
    public class DefaultController : Controller
    {
        private readonly BlogcanhannContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        public DefaultController(BlogcanhannContext context, IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public ActionResult Login()
        {

            var session = _httpContextAccessor.HttpContext.Session;
            if (session.GetString("user") != null)
            {
                return RedirectToAction("Index", "Posts");
            }
            return View();

        }
        [HttpPost]
        public ActionResult Login(string email, string password)
        {
            var usr = email;
            var pass = password;

            // Tìm người dùng trong cơ sở dữ liệu bằng email
            var user = _context.Users.FirstOrDefault(x => x.Email == usr);

            if (user != null)
            {
                // Mật khẩu đã lưu trong cơ sở dữ liệu
                string hashedPasswordFromDatabase = user.Pass;

                // So sánh mật khẩu đã nhập từ form và mật khẩu đã được mã hóa từ cơ sở dữ liệu
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(pass, hashedPasswordFromDatabase);

                if (isPasswordValid)
                {
                    HttpContext.Session.SetString("userId", user.IdUser.ToString());
                    // Mật khẩu hợp lệ, kiểm tra vai trò của người dùng
                    if (user.IdRole == 1)
                    {
                        HttpContext.Session.SetString("user", email);
                        TempData["AlertMessage"] = "Mã thành viên: " + user.IdUser;
                        return RedirectToAction("Index", "Home1");
                    }
                    else if (user.IdRole == 2)
                    {
                        HttpContext.Session.SetString("user", email);
                        TempData["AlertMessage"] = "Mã thành viên: " + user.IdUser;
                        return RedirectToAction("Index", "Posts");
                    }
                }
            }

            // Người dùng không tồn tại hoặc mật khẩu không hợp lệ
            TempData["ErrorMessage"] = "Đăng nhập thất bại!";
            return RedirectToAction("Login");
        }
        [HttpGet]
        public ActionResult Logout()
        {
            // Xóa session khi đăng xuất
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register([Bind("IdUser,FullName,PhoneNumber,Email,DateOfBirth,Gender,PlaceOfBirth,CreatedAt,LastUpdatedAt,ExistenceStatus,Pass,IdRole")] User user)
        {

            // Mã hóa mật khẩu trước khi lưu vào CSDL
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Pass);
            var existingUser = _context.Users.FirstOrDefault(u => u.Email == user.Email);

            if (existingUser == null)
            {
                // Tạo đối tượng User mới và thêm vào cơ sở dữ liệu nếu chưa tồn tại
                var newUser = new User
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    Pass = hashedPassword,
                    IdRole = 2,
                    // Các thuộc tính khác bạn muốn thiết lập
                };

                // Thêm mới IdUser dựa trên giá trị lớn nhất hiện tại trong cơ sở dữ liệu
                newUser.IdUser = _context.Users.Max(p => p.IdUser) + 1;

                _context.Users.Add(newUser);
                _context.SaveChanges();
                return RedirectToAction("RegistrationSuccess");
            }
            else
            {
                // Code xử lý khi ModelState không hợp lệ

                // Trả về một giá trị hợp lệ
                return View("Login", "Default");
            }

        }



        public IActionResult RegistrationSuccess()
        {
            return View();
        }



        public IActionResult AccessDenied()
        {
            return View();
        }
        public IActionResult ChangePassword()
        {
            return View();
        }
        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword)
        {
            // Lấy thông tin người dùng hiện tại từ session hoặc bất kỳ phương thức nào khác
            var currentUserEmail = HttpContext.Session.GetString("user");

            // Kiểm tra xem người dùng có tồn tại không
            var currentUser = _context.Users.FirstOrDefault(u => u.Email == currentUserEmail);

            if (currentUser != null)
            {
                // Kiểm tra mật khẩu hiện tại
                if (BCrypt.Net.BCrypt.Verify(currentPassword, currentUser.Pass))
                {
                    // Kiểm tra tính hợp lệ của mật khẩu mới
                    if (IsPasswordValid(newPassword))
                    {
                        // Mã hóa mật khẩu mới
                        string hashedNewPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                        // Cập nhật mật khẩu mới trong cơ sở dữ liệu
                        currentUser.Pass = hashedNewPassword;
                        _context.SaveChanges();

                        // Chuyển hướng hoặc trả về thông báo thành công
                        return RedirectToAction("Index", "Posts");
                    }
                    else
                    {
                        // Trả về thông báo lỗi nếu mật khẩu mới không hợp lệ
                        TempData["ErrorMessage"] = "Mật khẩu mới không hợp lệ!";
                    }
                }
                else
                {
                    // Trả về thông báo lỗi nếu mật khẩu hiện tại không chính xác
                    TempData["ErrorMessage"] = "Mật khẩu hiện tại không chính xác!";
                }
            }
            else
            {
                // Trả về thông báo lỗi nếu người dùng không tồn tại
                TempData["ErrorMessage"] = "Người dùng không tồn tại!";
            }

            // Chuyển hướng hoặc trả về trang Change Password nếu có lỗi
            return RedirectToAction("ChangePassword");
        }
        // Phương thức kiểm tra tính hợp lệ của mật khẩu mới
        private bool IsPasswordValid(string newPassword)
        {
            // Kiểm tra độ dài tối thiểu của mật khẩu
            if (newPassword.Length < 8)
            {
                return false; // Mật khẩu quá ngắn
            }

            // Kiểm tra xem mật khẩu có chứa ít nhất một ký tự số không
            bool hasNumber = false;
            foreach (char c in newPassword)
            {
                if (char.IsDigit(c))
                {
                    hasNumber = true;
                    break;
                }
            }

            if (!hasNumber)
            {
                return false; // Mật khẩu không chứa số
            }

            // Kiểm tra xem mật khẩu có chứa ít nhất một ký tự in hoa không
            bool hasUpperCase = false;
            foreach (char c in newPassword)
            {
                if (char.IsUpper(c))
                {
                    hasUpperCase = true;
                    break;
                }
            }

            if (!hasUpperCase)
            {
                return false; // Mật khẩu không chứa chữ in hoa
            }

            // Nếu tất cả các điều kiện đều được đáp ứng, mật khẩu được coi là hợp lệ
            return true;
        }
        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                // Tạo token đặt lại mật khẩu
                string resetToken = GenerateResetToken();

                // Lưu token đặt lại mật khẩu vào bộ nhớ cache với email người dùng làm khóa
                _cache.Set(email, resetToken, TimeSpan.FromMinutes(30)); // Thời gian hết hạn: 30 phút
                
                // Gửi email đặt lại mật khẩu với token
                SendResetPasswordEmail(email, resetToken);

                // Chuyển hướng đến trang thông báo gửi email thành công
                return RedirectToAction("ResetPasswordEmailSent");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Không tồn tại người dùng với địa chỉ email đã cung cấp!");
                return View();
            }
        }
        private string GenerateResetToken()
        {
            // Thực hiện logic tạo token ở đây (ví dụ: sử dụng GUID hoặc bất kỳ cơ chế nào khác)
            string resetToken = Guid.NewGuid().ToString();
            return resetToken;
        }

        private void SendResetPasswordEmail(string userEmail, string resetToken)
        {


            try
            {
                // Thông tin cấu hình SMTP
                string smtpServer = "smtp.gmail.com";
                int smtpPort = 587; // Port mặc định cho SMTP
                string smtpUsername = "tranthebaobt8@gmail.com";
                string smtpPassword = "0378060941T";

                // Tạo đối tượng MailMessage
                using (MailMessage mailMessage = new MailMessage())
                {
                    // Địa chỉ email người gửi
                    mailMessage.From = new MailAddress("your_email@example.com");

                    // Địa chỉ email người nhận
                    mailMessage.To.Add(new MailAddress(userEmail));

                    // Tiêu đề email
                    mailMessage.Subject = "Đặt lại mật khẩu";

                    // Nội dung email
                    mailMessage.Body = $"Nhấp vào liên kết sau để đặt lại mật khẩu của bạn:\n\n" +
                                        $"https://example.com/resetpassword?token={resetToken}";

                    // Gửi email dưới dạng văn bản thuần túy
                    mailMessage.IsBodyHtml = false;

                    // Cấu hình thông tin SMTP và gửi email
                    using (SmtpClient smtpClient = new SmtpClient(smtpServer, smtpPort))
                    {
                        smtpClient.EnableSsl = true; // Sử dụng SSL để bảo mật kết nối
                        smtpClient.UseDefaultCredentials = false; // Không sử dụng thông tin xác thực mặc định
                        smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword); // Thông tin xác thực SMTP

                        smtpClient.Send(mailMessage); // Gửi email
                    }
                }
            }
            catch (Exception ex)
            {
                // Xử lý các trường hợp ngoại lệ khi gửi email không thành công
                Console.WriteLine($"Lỗi khi gửi email đặt lại mật khẩu: {ex.Message}");
            }
        }


        public IActionResult ResetPasswordEmailSent()
        {
            return View();
        }
    }
}


  
