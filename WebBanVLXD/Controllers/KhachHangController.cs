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

        // ==========================
        // HIỂN THỊ GIỎ HÀNG
        // ==========================
        public ActionResult GioHang()
        {
            var cart = Session["Cart"] as List<SANPHAM> ?? new List<SANPHAM>();
            return View(cart);
        }

        // ==========================
        // THÊM SẢN PHẨM VÀO GIỎ HÀNG
        // ==========================
        [HttpPost]
        public ActionResult ThemVaoGio(FormCollection form)
        {
            string id = form["MaSP"];
            int soLuong = Convert.ToInt32(form["SoLuong"]);

            if (string.IsNullOrEmpty(id))
                return RedirectToAction("Index", "SanPham");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM SANPHAM WHERE MaSP=@id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    var sp = new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonGia = Convert.ToDecimal(rd["DonGia"]),
                        SoLuongTon = soLuong,
                        HinhAnh = rd["HinhAnh"].ToString()
                    };

                    var cart = Session["Cart"] as List<SANPHAM> ?? new List<SANPHAM>();

                    var existing = cart.FirstOrDefault(x => x.MaSP == sp.MaSP);
                    if (existing != null)
                        existing.SoLuongTon += soLuong;
                    else
                        cart.Add(sp);

                    Session["Cart"] = cart;
                }
            }

            TempData["ThongBao"] = "Đã thêm vào giỏ hàng!";
            return RedirectToAction("GioHang");
        }


        // ==========================
        // XÓA SẢN PHẨM KHỎI GIỎ
        // ==========================
        public ActionResult XoaKhoiGio(string id)
        {
            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart != null)
            {
                cart.RemoveAll(x => x.MaSP == id);
                Session["Cart"] = cart;
            }
            return RedirectToAction("GioHang");
        }

        // ==========================
        // CẬP NHẬT SỐ LƯỢNG TRONG GIỎ
        // ==========================
        [HttpPost]
        public ActionResult CapNhatGio(string id, int soLuong)
        {
            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart != null)
            {
                var sp = cart.FirstOrDefault(x => x.MaSP == id);
                if (sp != null)
                    sp.SoLuongTon = soLuong;
                Session["Cart"] = cart;
            }
            return RedirectToAction("GioHang");
        }

        // ==========================
        // HIỂN THỊ TRANG XÁC NHẬN ĐẶT HÀNG
        // ==========================
        public ActionResult DatHang()
        {
            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "SanPham");

            ViewBag.TongTien = cart.Sum(x => x.DonGia * x.SoLuongTon);
            return View(cart);
        }

        // ==========================
        // XỬ LÝ ĐẶT HÀNG
        // ==========================
        [HttpPost]
        public ActionResult XacNhanDatHang(string phuongThuc)
        {
            var cart = Session["Cart"] as List<SANPHAM>;
            if (cart == null || !cart.Any())
                return RedirectToAction("Index", "SanPham");

            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            string maKH = Session["UserID"].ToString();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    // 1️⃣ Tạo hóa đơn mới
                    string sqlHD = @"
                        INSERT INTO HOADON (NgayLap, TongTien, TrangThai, PhuongThucTT, MaKH, MaQL)
                        VALUES (GETDATE(), @tong, N'Đã thanh toán', @pt, @maKH, 'US002')";
                    SqlCommand cmdHD = new SqlCommand(sqlHD, conn, tran);
                    cmdHD.Parameters.AddWithValue("@tong", cart.Sum(x => x.DonGia * x.SoLuongTon));
                    cmdHD.Parameters.AddWithValue("@pt", phuongThuc ?? "Tiền mặt");
                    cmdHD.Parameters.AddWithValue("@maKH", maKH);
                    cmdHD.ExecuteNonQuery();

                    // 2️⃣ Lấy mã hóa đơn vừa tạo
                    string maHD = "";
                    SqlCommand cmdGet = new SqlCommand("SELECT TOP 1 MaHD FROM HOADON ORDER BY MaHD DESC", conn, tran);
                    maHD = cmdGet.ExecuteScalar().ToString();

                    // 3️⃣ Thêm chi tiết hóa đơn & trừ tồn kho
                    foreach (var sp in cart)
                    {
                        SqlCommand cmdCT = new SqlCommand("INSERT INTO CTHOADON VALUES (@maHD,@maSP,@sl,@dg)", conn, tran);
                        cmdCT.Parameters.AddWithValue("@maHD", maHD);
                        cmdCT.Parameters.AddWithValue("@maSP", sp.MaSP);
                        cmdCT.Parameters.AddWithValue("@sl", sp.SoLuongTon);
                        cmdCT.Parameters.AddWithValue("@dg", sp.DonGia);
                        cmdCT.ExecuteNonQuery();

                        SqlCommand cmdUpd = new SqlCommand("UPDATE SANPHAM SET SoLuongTon = SoLuongTon - @sl WHERE MaSP=@maSP", conn, tran);
                        cmdUpd.Parameters.AddWithValue("@sl", sp.SoLuongTon);
                        cmdUpd.Parameters.AddWithValue("@maSP", sp.MaSP);
                        cmdUpd.ExecuteNonQuery();
                    }

                    tran.Commit();
                    Session["Cart"] = null;
                    TempData["ThongBao"] = "Đặt hàng thành công!";
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

        // ==========================
        // XEM LỊCH SỬ ĐƠN HÀNG CỦA KHÁCH
        // ==========================
        public ActionResult DonHangCuaToi()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Account");

            string maKH = Session["UserID"].ToString();
            List<HOADON> dsHD = new List<HOADON>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM HOADON WHERE MaKH=@maKH ORDER BY NgayLap DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maKH", maKH);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    dsHD.Add(new HOADON
                    {
                        MaHD = rd["MaHD"].ToString(),
                        NgayLap = Convert.ToDateTime(rd["NgayLap"]),
                        TongTien = Convert.ToDecimal(rd["TongTien"]),
                        TrangThai = rd["TrangThai"].ToString(),
                        PhuongThucTT = rd["PhuongThucTT"].ToString()
                    });
                }
            }

            return View(dsHD);
        }

        // Lịch sử mua hàng
        public ActionResult LichSuMuaHang()
        {
            string maKH = Session["UserID"]?.ToString();

            if (string.IsNullOrEmpty(maKH))
                return RedirectToAction("Login", "Account");

            List<Dictionary<string, object>> dsHoaDon = new List<Dictionary<string, object>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
            SELECT MaHD, NgayLap, TongTien, PhuongThucTT, TrangThai
            FROM HOADON
            WHERE MaKH = @MaKH
            ORDER BY NgayLap DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MaKH", maKH);
                conn.Open();

                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var item = new Dictionary<string, object>();
                    item["MaHD"] = rd["MaHD"];
                    item["NgayLap"] = Convert.ToDateTime(rd["NgayLap"]).ToString("dd/MM/yyyy");
                    item["TongTien"] = Convert.ToDecimal(rd["TongTien"]).ToString("N0") + " đ";
                    item["PhuongThucTT"] = rd["PhuongThucTT"].ToString();
                    item["TrangThai"] = rd["TrangThai"].ToString();
                    dsHoaDon.Add(item);
                }
            }

            return View(dsHoaDon);
        }


        public ActionResult ChiTietHoaDon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("LichSuMuaHang");

            string maKH = Session["UserID"]?.ToString();
            if (string.IsNullOrEmpty(maKH))
                return RedirectToAction("Login", "Account");

            List<Dictionary<string, object>> chiTiet = new List<Dictionary<string, object>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
            SELECT C.MaSP, S.TenSP, C.SoLuong, C.DonGia, 
                   (C.SoLuong * C.DonGia) AS ThanhTien
            FROM CTHOADON C
            JOIN SANPHAM S ON C.MaSP = S.MaSP
            WHERE C.MaHD = @MaHD";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MaHD", id);
                conn.Open();

                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var item = new Dictionary<string, object>();
                    item["MaSP"] = rd["MaSP"].ToString();
                    item["TenSP"] = rd["TenSP"].ToString();
                    item["SoLuong"] = rd["SoLuong"].ToString();
                    item["DonGia"] = Convert.ToDecimal(rd["DonGia"]).ToString("N0") + " đ";
                    item["ThanhTien"] = Convert.ToDecimal(rd["ThanhTien"]).ToString("N0") + " đ";
                    chiTiet.Add(item);
                }
            }

            ViewBag.MaHD = id;
            return View(chiTiet);
        }



    }
}
