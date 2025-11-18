using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Caching;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Net.Mail;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class AccountController : Controller
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;
        private static readonly ObjectCache TokenCache = MemoryCache.Default;
        private const int TokenExpirationHours = 1;

        // ===========================
        // LOGIN
        // ===========================
        public ActionResult Login()
        {
            if (TempData["ResetSuccess"] != null)
                ViewBag.ResetSuccess = TempData["ResetSuccess"];

            return View();
        }

        [HttpPost]
        public ActionResult Login(string Email, string MatKhau)
        {
            NGUOIDUNG user = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT MaUser, TenNguoiDung, Role, TrangThai 
                               FROM NGUOIDUNG
                               WHERE Email = @Email AND MatKhau = @MatKhau";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@MatKhau", MatKhau);

                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    user = new NGUOIDUNG
                    {
                        MaUser = rd["MaUser"].ToString(),
                        TenNguoiDung = rd["TenNguoiDung"].ToString(),
                        Role = rd["Role"].ToString(),
                        TrangThai = rd["TrangThai"].ToString()
                    };
                }
            }

            if (user == null)
            {
                ViewBag.Error = "Sai email hoặc mật khẩu!";
                return View();
            }

            if (user.TrangThai != null && user.TrangThai.Trim().ToLower() == "khoa")
            {
                ViewBag.Error = "Tài khoản của bạn đã bị khóa!";
                return View();
            }

            Session["UserID"] = user.MaUser;
            Session["UserName"] = user.TenNguoiDung;
            Session["Role"] = user.Role;

            return RedirectToAction("Index", "SanPham");
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // ===========================
        // REGISTER
        // ===========================
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

            // Kiểm tra email tồn tại
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string check = "SELECT COUNT(*) FROM NGUOIDUNG WHERE Email=@Email";
                SqlCommand cmd = new SqlCommand(check, conn);
                cmd.Parameters.AddWithValue("@Email", Email);

                conn.Open();
                if ((int)cmd.ExecuteScalar() > 0)
                {
                    ModelState.AddModelError("Email", "Email đã được sử dụng.");
                    return View();
                }
            }

            // Tạo mã user tự động
            string maUser;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string q = "SELECT COUNT(*) FROM NGUOIDUNG";
                SqlCommand cmd = new SqlCommand(q, conn);

                conn.Open();
                int count = (int)cmd.ExecuteScalar() + 1;
                maUser = "US" + count.ToString("D4");
            }

            // Thêm user mới
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO NGUOIDUNG 
                                (MaUser, TenNguoiDung, MatKhau, Email, SDT, DiaChi, Role, TrangThai, NgayTao)
                                VALUES
                                (@MaUser, @TenNguoiDung, @MatKhau, @Email, @SDT, @DiaChi, 'khach', 'HoatDong', GETDATE())";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MaUser", maUser);
                cmd.Parameters.AddWithValue("@TenNguoiDung", TenNguoiDung);
                cmd.Parameters.AddWithValue("@MatKhau", MatKhau);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@SDT",
                    string.IsNullOrWhiteSpace(SDT) ? (object)DBNull.Value : SDT);
                cmd.Parameters.AddWithValue("@DiaChi",
                    string.IsNullOrWhiteSpace(DiaChi) ? (object)DBNull.Value : DiaChi);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["RegisterSuccess"] = "Đăng ký thành công!";
            return RedirectToAction("Login");
        }

        // ===========================
        // SEND OTP (Forgot Password)
        // ===========================
        [HttpPost]
        public JsonResult SendOtp()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    body = reader.ReadToEnd();
                }

                string email = null;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var js = new JavaScriptSerializer();
                    var dict = js.Deserialize<Dictionary<string, string>>(body);
                    if (dict != null && dict.ContainsKey("Email"))
                        email = dict["Email"];
                }

                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email không được để trống." });

                string ten = null;
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string sql = "SELECT TenNguoiDung FROM NGUOIDUNG WHERE Email=@Email AND TrangThai='HoatDong'";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Email", email);

                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result != null) ten = result.ToString();
                }

                if (ten == null)
                    return Json(new { success = false, message = "Email không tồn tại." });

                // Tạo OTP
                string otp = new Random().Next(100000, 999999).ToString();
                TokenCache.Set($"otp:{email.ToLower()}", otp,
                    new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) });

                SendEmail(email, "Mã OTP - Đổi mật khẩu",
                    $"Xin chào {ten},<br/>Mã OTP của bạn là: <b>{otp}</b>");

                return Json(new { success = true, message = "Đã gửi OTP!" });
            }
            catch
            {
                return Json(new { success = false, message = "Lỗi hệ thống!" });
            }
        }

        // ===========================
        // VERIFY OTP
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOtp(string Email, string Otp)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Otp))
            {
                ModelState.AddModelError("", "Email và OTP là bắt buộc.");
                return View("ForgotPassword");
            }

            string cached = TokenCache.Get($"otp:{Email.ToLower()}") as string;
            if (cached == null || cached != Otp)
            {
                ModelState.AddModelError("", "OTP không đúng.");
                return View("ForgotPassword");
            }

            string maUser = null;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaUser FROM NGUOIDUNG WHERE Email=@Email AND TrangThai='HoatDong'";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);

                conn.Open();
                maUser = cmd.ExecuteScalar()?.ToString();
            }

            if (maUser == null)
            {
                ModelState.AddModelError("", "Không tìm thấy tài khoản.");
                return View("ForgotPassword");
            }

            string resetToken = Guid.NewGuid().ToString("N");
            TokenCache.Set(resetToken, maUser,
                new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(1) });

            TokenCache.Remove($"otp:{Email.ToLower()}");

            return RedirectToAction("ResetPassword", new { token = resetToken });
        }

        // ===========================
        // RESET PASSWORD
        // ===========================
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token) || !TokenCache.Contains(token))
            {
                ViewBag.Error = "Link không hợp lệ.";
                return View("ResetPasswordInvalid");
            }

            return View(new DoiMatKhau { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(DoiMatKhau model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!TokenCache.Contains(model.Token))
            {
                ModelState.AddModelError("", "Link hết hạn.");
                return View(model);
            }

            string maUser = TokenCache.Get(model.Token) as string;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE NGUOIDUNG SET MatKhau=@MatKhau WHERE MaUser=@MaUser";
                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MatKhau", model.MatKhauMoi);
                cmd.Parameters.AddWithValue("@MaUser", maUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TokenCache.Remove(model.Token);
            TempData["ResetSuccess"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Login");
        }

        // ===========================
        // SEND EMAIL (Internal)
        // ===========================
        private void SendEmail(string toEmail, string subject, string bodyHtml)
        {
            var smtp = ConfigurationManager.GetSection("system.net/mailSettings/smtp")
                        as System.Net.Configuration.SmtpSection;

            using (var msg = new MailMessage(smtp.From, toEmail))
            {
                msg.Subject = subject;
                msg.Body = bodyHtml;
                msg.IsBodyHtml = true;

                using (var client = new SmtpClient())
                    client.Send(msg);
            }
        }

        // ===========================
        // VIEW PROFILE
        // ===========================
        public ActionResult ThongTin()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            string maUser = Session["UserID"].ToString();
            NGUOIDUNG user = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM NGUOIDUNG WHERE MaUser=@MaUser";
                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MaUser", maUser);

                conn.Open();
                var rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    user = new NGUOIDUNG
                    {
                        MaUser = rd["MaUser"].ToString(),
                        TenNguoiDung = rd["TenNguoiDung"].ToString(),
                        Email = rd["Email"].ToString(),
                        SDT = rd["SDT"].ToString(),
                        DiaChi = rd["DiaChi"].ToString(),
                        Role = rd["Role"].ToString()
                    };
                }
            }

            return View(user);
        }

        // ===========================
        // EDIT PROFILE
        // ===========================
        public ActionResult SuaThongTin()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            string maUser = Session["UserID"].ToString();
            NGUOIDUNG user = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM NGUOIDUNG WHERE MaUser=@MaUser";
                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MaUser", maUser);

                conn.Open();
                var rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    user = new NGUOIDUNG
                    {
                        MaUser = rd["MaUser"].ToString(),
                        TenNguoiDung = rd["TenNguoiDung"].ToString(),
                        Email = rd["Email"].ToString(),
                        SDT = rd["SDT"].ToString(),
                        DiaChi = rd["DiaChi"].ToString(),
                        Role = rd["Role"].ToString()
                    };
                }
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SuaThongTin(NGUOIDUNG model)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login");

            if (!ModelState.IsValid)
                return View(model);

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"UPDATE NGUOIDUNG 
                               SET TenNguoiDung=@TenNguoiDung,
                                   SDT=@SDT,
                                   DiaChi=@DiaChi
                               WHERE MaUser=@MaUser";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@TenNguoiDung", model.TenNguoiDung);
                cmd.Parameters.AddWithValue("@SDT",
                    string.IsNullOrWhiteSpace(model.SDT) ? (object)DBNull.Value : model.SDT);
                cmd.Parameters.AddWithValue("@DiaChi",
                    string.IsNullOrWhiteSpace(model.DiaChi) ? (object)DBNull.Value : model.DiaChi);
                cmd.Parameters.AddWithValue("@MaUser", model.MaUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["UpdateSuccess"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("ThongTin");
        }
    }
}
