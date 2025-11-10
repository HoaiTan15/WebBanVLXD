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
        VLXD_DBEntities db = new VLXD_DBEntities();

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
                    return RedirectToAction("TrangChu", "Home");
                else if (user.Role == "quanly")
                    return RedirectToAction("Index", "SanPham");
                else
                    return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Sai email hoặc mật khẩu!";
            return View();
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // ----------------- FORGOT PASSWORD (view) -----------------
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // ----------------- Send OTP (AJAX) -----------------
        // Accepts JSON body { "Email": "user@example.com" } or form post
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                // Try parse JSON body
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    try
                    {
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, string>>(requestBody);
                        if (dict != null && dict.ContainsKey("Email"))
                            email = dict["Email"];
                    }
                    catch
                    {
                        // ignore parsing errors
                    }
                }

                // fallback to form data
                if (string.IsNullOrEmpty(email))
                    email = Request.Form["Email"];

                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email không được để trống." });

                var account = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == email && u.TrangThai == "HoatDong");
                if (account == null)
                {
                    // Do not reveal whether email exists — return generic success
                    return Json(new { success = true, message = "Nếu email tồn tại, mã OTP sẽ được gửi trong vài phút." });
                }

                // generate OTP and cache it (10 minutes)
                var otp = new Random().Next(100000, 999999).ToString();
                var policyOtp = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) };
                TokenCache.Set($"otp:{email.ToLowerInvariant()}", otp, policyOtp);

                // optional: also store attempts/count to rate-limit (not implemented)

                var body = $@"Xin chào {account.TenNguoiDung},<br/>
                              Mã xác thực (OTP) của bạn là: <strong>{otp}</strong><br/>
                              Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.";

                try
                {
                    SendEmail(account.Email, "Mã xác thực (OTP) - Đổi mật khẩu", body);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SendEmail error: " + ex.Message);
                    // still return generic success so we don't reveal info
                    return Json(new { success = true, message = "Yêu cầu đã được gửi (hoặc đang chờ xử lý)." });
                }

                return Json(new { success = true, message = "Mã OTP đã được gửi. Kiểm tra email." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendOtp error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi khi gửi mã OTP. Thử lại sau." });
            }
        }

        // ----------------- Verify OTP (form submit) -----------------
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

            // OTP valid -> create reset token (re-use existing ResetPassword flow)
            var account = db.NGUOIDUNGs.FirstOrDefault(u => u.Email == Email && u.TrangThai == "HoatDong");
            if (account == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View("ForgotPassword");
            }

            var resetToken = Guid.NewGuid().ToString("N");
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(TokenExpirationHours) };
            // store mapping token -> MaUser (so ResetPassword can use it)
            TokenCache.Set(resetToken, account.MaUser, policy);

            // remove used OTP
            TokenCache.Remove($"otp:{Email.ToLowerInvariant()}");

            // redirect to ResetPassword with token
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

            var account = db.NGUOIDUNGs.Find(maUser);
            if (account == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View(model);
            }

            account.MatKhau = model.MatKhauMoi;
            db.SaveChanges();
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
    }
}
