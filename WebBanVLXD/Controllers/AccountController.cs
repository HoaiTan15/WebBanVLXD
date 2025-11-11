using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanVLXD.Models;
using System.Runtime.Caching;
using System.Net.Mail;
using System.Configuration;
using System.Web.Script.Serialization;

namespace WebBanVLXD.Controllers
{
    public class AccountController : Controller
    {

        VLXD_DBDataContext db = new VLXD_DBDataContext(
            System.Configuration.ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString
        );

        private static readonly ObjectCache TokenCache = MemoryCache.Default;
        private const int TokenExpirationHours = 1;

        // ----------------- LOGIN -----------------
        public ActionResult Login()
        {
            if (TempData["ResetSuccess"] != null)
                ViewBag.ResetSuccess = TempData["ResetSuccess"];
            return View();
        }

        [HttpPost]
        public ActionResult Login(string Email, string MatKhau)
        {
            var user = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == Email && u.MatKhau == MatKhau && u.TrangThai == "HoatDong");
            if (user != null)
            {
                Session["UserID"] = user.MaUser;
                Session["UserName"] = user.TenNguoiDung;
                Session["Role"] = user.Role;

                if (user.Role == "admin")
                    return RedirectToAction("Index", "SanPham");
                else if (user.Role == "quanly")
                    return RedirectToAction("Index", "SanPham");
                else
                    return RedirectToAction("Index", "SanPham");
            }

            ViewBag.Error = "Sai email hoặc mật khẩu!";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // ----------------- REGISTER -----------------
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string TenNguoiDung, string Email, string MatKhau, string XacNhan, string SDT, string DiaChi)
        {
            if (string.IsNullOrWhiteSpace(TenNguoiDung))
                ModelState.AddModelError("TenNguoiDung", "Họ tên là bắt buộc.");
            if (string.IsNullOrWhiteSpace(Email))
                ModelState.AddModelError("Email", "Email là bắt buộc.");
            if (string.IsNullOrWhiteSpace(MatKhau))
                ModelState.AddModelError("MatKhau", "Mật khẩu là bắt buộc.");
            if (MatKhau != XacNhan)
                ModelState.AddModelError("XacNhan", "Mật khẩu xác nhận không khớp.");

            if (!ModelState.IsValid)
                return View();

            var existing = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                return View();
            }

            // ======= Tạo mã người dùng tự động (US0001, US0002, ...) =======
            int count = db.NGUOIDUNGs.Count() + 1;
            string maUser = "US" + count.ToString("D4");

            // ======= Tạo tài khoản mới =======
            var user = new NGUOIDUNG
            {
                MaUser = maUser,
                TenNguoiDung = TenNguoiDung,
                Email = Email,
                MatKhau = MatKhau,
                SDT = SDT,
                DiaChi = DiaChi,
                Role = "khach",
                TrangThai = "HoatDong",
                NgayTao = DateTime.Now
            };

            db.NGUOIDUNGs.InsertOnSubmit(user);
            db.SubmitChanges();

            TempData["RegisterSuccess"] = "Đăng ký thành công! Hãy đăng nhập.";
            return RedirectToAction("Login");
        }

        // ----------------- FORGOT PASSWORD (view) -----------------
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // ----------------- Send OTP (AJAX) -----------------
        [HttpPost]
        public JsonResult SendOtp()
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    requestBody = reader.ReadToEnd();
                }

                string email = null;
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    try
                    {
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, string>>(requestBody);
                        if (dict != null && dict.ContainsKey("Email"))
                            email = dict["Email"];
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(email))
                    email = Request.Form["Email"];

                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email không được để trống." });

                var account = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == email && u.TrangThai == "HoatDong");
                if (account == null)
                {
                    return Json(new { success = true, message = "Nếu email tồn tại, mã OTP sẽ được gửi trong vài phút." });
                }

                var otp = new Random().Next(100000, 999999).ToString();
                var policyOtp = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) };
                TokenCache.Set($"otp:{email.ToLowerInvariant()}", otp, policyOtp);

                var body = $@"Xin chào {account.TenNguoiDung},<br/>
                              Mã xác thực (OTP) của bạn là: <strong>{otp}</strong><br/>
                              Mã có hiệu lực trong 10 phút.";

                try
                {
                    SendEmail(account.Email, "Mã xác thực (OTP) - Đổi mật khẩu", body);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SendEmail error: " + ex.Message);
                }

                return Json(new { success = true, message = "Mã OTP đã được gửi. Kiểm tra email." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendOtp error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi khi gửi mã OTP. Thử lại sau." });
            }
        }

        // ----------------- Verify OTP -----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOtp(string Email, string Otp)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Otp))
            {
                ModelState.AddModelError("", "Email và mã OTP là bắt buộc.");
                return View("ForgotPassword");
            }

            var cached = TokenCache.Get($"otp:{Email.ToLowerInvariant()}") as string;
            if (string.IsNullOrEmpty(cached) || cached != Otp.Trim())
            {
                ModelState.AddModelError("", "Mã OTP không đúng hoặc đã hết hạn.");
                return View("ForgotPassword");
            }

            var account = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == Email && u.TrangThai == "HoatDong");
            if (account == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View("ForgotPassword");
            }

            var resetToken = Guid.NewGuid().ToString("N");
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(TokenExpirationHours) };
            TokenCache.Set(resetToken, account.MaUser, policy);
            TokenCache.Remove($"otp:{Email.ToLowerInvariant()}");

            return RedirectToAction("ResetPassword", new { token = resetToken });
        }

        // ----------------- RESET PASSWORD -----------------
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token) || !TokenCache.Contains(token))
            {
                ViewBag.Error = "Link không hợp lệ hoặc đã hết hạn.";
                return View("ResetPasswordInvalid");
            }

            var model = new DoiMatKhau { Token = token };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(DoiMatKhau model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrEmpty(model.Token) || !TokenCache.Contains(model.Token))
            {
                ModelState.AddModelError("", "Link không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            var maUser = TokenCache.Get(model.Token) as string;
            if (string.IsNullOrEmpty(maUser))
            {
                ModelState.AddModelError("", "Token không hợp lệ.");
                return View(model);
            }

            var account = db.NGUOIDUNGs.SingleOrDefault(u => u.MaUser == maUser);
            if (account == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View(model);
            }

            account.MatKhau = model.MatKhauMoi;
            db.SubmitChanges();
            TokenCache.Remove(model.Token);

            TempData["ResetSuccess"] = "Mật khẩu đã được cập nhật. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // ----------------- EMAIL SENDER -----------------
        private void SendEmail(string toEmail, string subject, string bodyHtml)
        {
            var smtpSection = ConfigurationManager.GetSection("system.net/mailSettings/smtp") as System.Net.Configuration.SmtpSection;
            if (smtpSection != null && smtpSection.Network != null)
            {
                var from = smtpSection.From;
                using (var msg = new MailMessage(from, toEmail))
                {
                    msg.Subject = subject;
                    msg.Body = bodyHtml;
                    msg.IsBodyHtml = true;

                    using (var client = new SmtpClient())
                    {
                        client.Send(msg);
                    }
                }
            }
        }

        // ----------------- TEST EMAIL -----------------
        public ActionResult TestEmail()
        {
            try
            {
                SendEmail("herophan1503@gmail.com", "Test OTP", "<b>Mail thử nghiệm gửi từ WebBanVLXD</b>");
                return Content("Gửi mail thành công! Hãy kiểm tra hộp thư đến của bạn.");
            }
            catch (Exception ex)
            {
                return Content("Lỗi khi gửi mail: " + ex.Message);
            }
        }
    }
}
