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
            NGUOIDUNG user = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM NGUOIDUNG WHERE Email=@Email AND MatKhau=@MatKhau AND TrangThai='HoatDong'";
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
                        Role = rd["Role"].ToString()
                    };
                }
            }

            if (user != null)
            {
                Session["UserID"] = user.MaUser;
                Session["UserName"] = user.TenNguoiDung;
                Session["Role"] = user.Role;

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

            // Kiểm tra trùng email
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string check = "SELECT COUNT(*) FROM NGUOIDUNG WHERE Email=@Email";
                SqlCommand cmd = new SqlCommand(check, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                conn.Open();

                int exist = (int)cmd.ExecuteScalar();
                if (exist > 0)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                    return View();
                }
            }

            // Tạo mã người dùng tự động (US0001, US0002, ...)
            string maUser;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string countQuery = "SELECT COUNT(*) FROM NGUOIDUNG";
                SqlCommand cmd = new SqlCommand(countQuery, conn);
                conn.Open();
                int count = (int)cmd.ExecuteScalar() + 1;
                maUser = "US" + count.ToString("D4");
            }

            // Thêm người dùng mới
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string insert = @"INSERT INTO NGUOIDUNG (MaUser, TenNguoiDung, MatKhau, Email, SDT, DiaChi, Role, TrangThai, NgayTao)
                                  VALUES (@MaUser, @TenNguoiDung, @MatKhau, @Email, @SDT, @DiaChi, 'khach', 'HoatDong', GETDATE())";
                SqlCommand cmd = new SqlCommand(insert, conn);
                cmd.Parameters.AddWithValue("@MaUser", maUser);
                cmd.Parameters.AddWithValue("@TenNguoiDung", TenNguoiDung);
                cmd.Parameters.AddWithValue("@MatKhau", MatKhau);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@SDT", (object)SDT ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DiaChi", (object)DiaChi ?? DBNull.Value);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["RegisterSuccess"] = "Đăng ký thành công! Hãy đăng nhập.";
            return RedirectToAction("Login");
        }

        // ----------------- FORGOT PASSWORD -----------------
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public JsonResult SendOtp()
        {
            try
            {
                // Đọc dữ liệu từ AJAX (Email)
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

                // Kiểm tra email có tồn tại hay không
                string tenNguoiDung = null;
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string q = "SELECT TenNguoiDung FROM NGUOIDUNG WHERE Email=@Email AND TrangThai='HoatDong'";
                    SqlCommand cmd = new SqlCommand(q, conn);
                    cmd.Parameters.AddWithValue("@Email", email);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        tenNguoiDung = result.ToString();
                }

                // Email không tồn tại trong hệ thống
                if (tenNguoiDung == null)
                {
                    return Json(new { success = false, message = "Email này không tồn tại trong hệ thống. Vui lòng kiểm tra lại." });
                }

                // Tạo OTP và lưu tạm
                var otp = new Random().Next(100000, 999999).ToString();
                var policyOtp = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) };
                TokenCache.Set($"otp:{email.ToLowerInvariant()}", otp, policyOtp);

                // Gửi email OTP
                var body = $@"Xin chào {tenNguoiDung},<br/>
                              Mã xác thực (OTP) của bạn là: <strong>{otp}</strong><br/>
                              Mã có hiệu lực trong 10 phút.";

                SendEmail(email, "Mã xác thực (OTP) - Đổi mật khẩu", body);
                return Json(new { success = true, message = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư (hoặc thư rác)." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendOtp error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi khi gửi mã OTP. Vui lòng thử lại sau." });
            }
        }

        // ----------------- VERIFY OTP -----------------
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

            string maUser = null;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaUser FROM NGUOIDUNG WHERE Email=@Email AND TrangThai='HoatDong'";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                conn.Open();
                var result = cmd.ExecuteScalar();
                if (result != null)
                    maUser = result.ToString();
            }

            if (maUser == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View("ForgotPassword");
            }

            var resetToken = Guid.NewGuid().ToString("N");
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(TokenExpirationHours) };
            TokenCache.Set(resetToken, maUser, policy);
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
                return Content("✅ Gửi mail thành công! Hãy kiểm tra hộp thư đến hoặc thư rác.");
            }
            catch (Exception ex)
            {
                return Content(" Lỗi khi gửi mail: " + ex.Message);
            }
        }
        // ----------------- XEM THÔNG TIN -----------------
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
                SqlDataReader rd = cmd.ExecuteReader();
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

        // ----------------- SỬA THÔNG TIN (GET) -----------------
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
                SqlDataReader rd = cmd.ExecuteReader();
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

        // ----------------- SỬA THÔNG TIN (POST) -----------------
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
                       SET TenNguoiDung=@TenNguoiDung, SDT=@SDT, DiaChi=@DiaChi 
                       WHERE MaUser=@MaUser";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@TenNguoiDung", model.TenNguoiDung ?? "");
                cmd.Parameters.AddWithValue("@SDT", model.SDT ?? "");
                cmd.Parameters.AddWithValue("@DiaChi", model.DiaChi ?? "");
                cmd.Parameters.AddWithValue("@MaUser", model.MaUser);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["UpdateSuccess"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("ThongTin");
        }


    }
}
