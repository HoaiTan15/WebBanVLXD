using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class KhachHangController : Controller
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // =============================
        // GIỎ HÀNG
        // =============================
        public ActionResult GioHang()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<SANPHAM> ?? new List<SANPHAM>();
            return View(cart);
        }

        // =============================
        // THÊM VÀO GIỎ
        // =============================
        [HttpPost]
        public ActionResult ThemVaoGio(FormCollection form)
        {
            if (Session["UserID"] == null)
            {
                TempData["Loi"] = "Bạn cần đăng nhập để thêm sản phẩm vào giỏ!";
                return RedirectToAction("Login", "Account");
            }

            string id = form["MaSP"];
            int soLuong = Convert.ToInt32(form["SoLuong"]);

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                string sql = "SELECT MaSP, TenSP, DonGia, SoLuongTon, HinhAnh FROM SANPHAM WHERE MaSP=@id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                SqlDataReader rd = cmd.ExecuteReader();
                if (!rd.Read())
                {
                    TempData["Loi"] = "Sản phẩm không tồn tại!";
                    return RedirectToAction("GioHang");
                }

                int tonKho = Convert.ToInt32(rd["SoLuongTon"]);
                if (soLuong > tonKho)
                {
                    TempData["Loi"] = $"Chỉ còn {tonKho} sản phẩm!";
                    return RedirectToAction("GioHang");
                }

                var cart = Session["Cart"] as List<SANPHAM> ?? new List<SANPHAM>();
                var existing = cart.FirstOrDefault(x => x.MaSP == id);

                if (existing != null)
                {
                    if (existing.SoLuongMua + soLuong > tonKho)
                    {
                        TempData["Loi"] = $"Vượt quá tồn kho! Chỉ còn {tonKho}.";
                        return RedirectToAction("GioHang");
                    }
                    existing.SoLuongMua += soLuong;
                }
                else
                {
                    cart.Add(new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonGia = Convert.ToDecimal(rd["DonGia"]),
                        SoLuongTon = tonKho,
                        HinhAnh = rd["HinhAnh"].ToString(),
                        SoLuongMua = soLuong
                    });
                }

                Session["Cart"] = cart;
            }

            TempData["ThongBao"] = "✔ Thêm vào giỏ thành công!";
            return RedirectToAction("GioHang");
        }

        // =============================
        // XÓA KHỎI GIỎ
        // =============================
        public ActionResult XoaKhoiGio(string id)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart != null)
            {
                cart.RemoveAll(x => x.MaSP.Trim() == id.Trim());
                Session["Cart"] = cart;
            }

            return RedirectToAction("GioHang");
        }

        // =============================
        // CẬP NHẬT GIỎ
        // =============================
        [HttpPost]
        public ActionResult CapNhatGio(string id, int soLuong)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart == null) return RedirectToAction("GioHang");

            var sp = cart.FirstOrDefault(x => x.MaSP == id);
            if (sp == null) return RedirectToAction("GioHang");

            int tonKho;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT SoLuongTon FROM SANPHAM WHERE MaSP=@id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                tonKho = (int)cmd.ExecuteScalar();
            }

            if (soLuong > tonKho)
            {
                TempData["Loi"] = $"Không đủ hàng! Chỉ còn {tonKho}.";
                return RedirectToAction("GioHang");
            }

            sp.SoLuongMua = soLuong;
            Session["Cart"] = cart;

            TempData["ThongBao"] = "Cập nhật giỏ hàng thành công!";
            return RedirectToAction("GioHang");
        }

        // =============================
        // ĐẶT HÀNG
        // =============================
        public ActionResult DatHang()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "SanPham");

            ViewBag.TongTien = cart.Sum(x => x.DonGia * x.SoLuongMua);
            return View(cart);
        }

        // =============================
        // XÁC NHẬN ĐẶT HÀNG
        // =============================
        [HttpPost]
        public ActionResult XacNhanDatHang(string phuongThuc)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "SanPham");

            string maKH = Session["UserID"].ToString();

            // Kiểm tra SDT và địa chỉ
            string sdt = null, diachi = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT SDT, DiaChi FROM NGUOIDUNG WHERE MaUser=@id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", maKH);

                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    sdt = rd["SDT"]?.ToString();
                    diachi = rd["DiaChi"]?.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(sdt) || string.IsNullOrWhiteSpace(diachi))
            {
                TempData["Loi"] = "Bạn cần cập nhật đầy đủ SỐ ĐIỆN THOẠI và ĐỊA CHỈ trước khi đặt hàng!";
                return RedirectToAction("ThongTin", "Account");
            }

            // Tạo hóa đơn
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    string sqlHD = @"INSERT INTO HOADON 
                                     (NgayLap, TongTien, TrangThai, PhuongThucTT, MaKH, MaQL)
                                     VALUES 
                                     (GETDATE(), @tong, N'Đã thanh toán', @pt, @maKH, 'US002')";

                    SqlCommand cmdHD = new SqlCommand(sqlHD, conn, tran);
                    cmdHD.Parameters.AddWithValue("@tong", cart.Sum(x => x.DonGia * x.SoLuongMua));
                    cmdHD.Parameters.AddWithValue("@pt", phuongThuc ?? "Tiền mặt");
                    cmdHD.Parameters.AddWithValue("@maKH", maKH);
                    cmdHD.ExecuteNonQuery();

                    string maHD = new SqlCommand("SELECT TOP 1 MaHD FROM HOADON ORDER BY MaHD DESC", conn, tran)
                                  .ExecuteScalar()
                                  .ToString();

                    foreach (var sp in cart)
                    {
                        new SqlCommand("INSERT INTO CTHOADON VALUES (@maHD,@maSP,@sl,@dg)", conn, tran)
                        {
                            Parameters =
                            {
                                new SqlParameter("@maHD", maHD),
                                new SqlParameter("@maSP", sp.MaSP),
                                new SqlParameter("@sl", sp.SoLuongMua),
                                new SqlParameter("@dg", sp.DonGia)
                            }
                        }.ExecuteNonQuery();

                        new SqlCommand("UPDATE SANPHAM SET SoLuongTon = SoLuongTon - @sl WHERE MaSP=@maSP", conn, tran)
                        {
                            Parameters =
                            {
                                new SqlParameter("@sl", sp.SoLuongMua),
                                new SqlParameter("@maSP", sp.MaSP)
                            }
                        }.ExecuteNonQuery();
                    }

                    tran.Commit();
                    Session["Cart"] = null;

                    TempData["ThongBao"] = "✔ Đặt hàng thành công!";
                    return RedirectToAction("DonHangCuaToi");
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    TempData["Loi"] = "Đặt hàng thất bại: " + ex.Message;
                    return RedirectToAction("GioHang");
                }
            }
        }

        // =============================
        // LỊCH SỬ ĐƠN HÀNG
        // =============================
        public ActionResult DonHangCuaToi()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            string maKH = Session["UserID"].ToString();
            var list = new List<HOADON>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM HOADON WHERE MaKH=@maKH ORDER BY NgayLap DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maKH", maKH);

                conn.Open();
                var rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    list.Add(new HOADON
                    {
                        MaHD = rd["MaHD"].ToString(),
                        NgayLap = Convert.ToDateTime(rd["NgayLap"]),
                        TongTien = Convert.ToDecimal(rd["TongTien"]),
                        TrangThai = rd["TrangThai"].ToString(),
                        PhuongThucTT = rd["PhuongThucTT"].ToString()
                    });
                }
            }

            return View(list);
        }

        // =============================
        // CHI TIẾT HÓA ĐƠN
        // =============================
        public ActionResult ChiTietHoaDon(string id)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(id))
                return RedirectToAction("DonHangCuaToi");

            var list = new List<Dictionary<string, object>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT C.MaSP, S.TenSP, C.SoLuong, C.DonGia,
                                      (C.SoLuong * C.DonGia) AS ThanhTien
                               FROM CTHOADON C
                               JOIN SANPHAM S ON C.MaSP = S.MaSP
                               WHERE C.MaHD = @maHD";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maHD", id);

                conn.Open();
                var rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    list.Add(new Dictionary<string, object>
                    {
                        { "MaSP", rd["MaSP"].ToString() },
                        { "TenSP", rd["TenSP"].ToString() },
                        { "SoLuong", rd["SoLuong"].ToString() },
                        { "DonGia", Convert.ToDecimal(rd["DonGia"]).ToString("N0") + " đ" },
                        { "ThanhTien", Convert.ToDecimal(rd["ThanhTien"]).ToString("N0") + " đ" }
                    });
                }
            }

            ViewBag.MaHD = id;
            return View(list);
        }

        // =============================
        // MUA NGAY
        // =============================
        [HttpPost]
        public ActionResult MuaNgay(string MaSP, int SoLuong)
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            var cart = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaSP, TenSP, DonGia, SoLuongTon, HinhAnh FROM SANPHAM WHERE MaSP=@id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", MaSP);

                conn.Open();
                var rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    cart.Add(new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonGia = Convert.ToDecimal(rd["DonGia"]),
                        SoLuongTon = Convert.ToInt32(rd["SoLuongTon"]),
                        HinhAnh = rd["HinhAnh"].ToString(),
                        SoLuongMua = SoLuong
                    });
                }
            }

            Session["Cart"] = cart;
            return RedirectToAction("DatHang");
        }
    }
}
