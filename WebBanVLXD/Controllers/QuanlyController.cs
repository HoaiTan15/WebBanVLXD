using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class QuanLyController : Controller
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // ================== TRANG CHÍNH CỦA QUẢN LÝ ==================
        public ActionResult Index()
        {
            // Kiểm tra vai trò đăng nhập
            if (Session["Role"] == null || Session["Role"].ToString() != "quanly")
            {
                return RedirectToAction("Login", "Account");
            }

            // Gửi cờ IsManager sang View
            ViewBag.IsManager = true;

            return View();
        }

        // ======================= QUẢN LÝ SẢN PHẨM =======================
        public ActionResult QLSanPham()
        {
            List<SANPHAM> sanPhams = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaSP, TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM FROM SANPHAM";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    sanPhams.Add(new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonGia = Convert.ToDecimal(rd["DonGia"]),
                        DonViTinh = rd["DonViTinh"].ToString(),
                        SoLuongTon = Convert.ToInt32(rd["SoLuongTon"]),
                        HinhAnh = rd["HinhAnh"].ToString(),
                        MoTa = rd["MoTa"].ToString(),
                        MaNCC = rd["MaNCC"].ToString(),
                        MaDM = rd["MaDM"].ToString()
                    });
                }
            }

            return View(sanPhams);
        }

        // ==================== THÊM SẢN PHẨM ====================
        [HttpPost]
        public ActionResult ThemSanPham(string ten, decimal dongia, string dvt, int sl, string mota, string mancc, string madm, string hinhanh)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "INSERT INTO SANPHAM (TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM) " +
                             "VALUES (@ten, @gia, @dvt, @sl, @hinh, @mota, @ncc, @dm)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ten", ten);
                cmd.Parameters.AddWithValue("@gia", dongia);
                cmd.Parameters.AddWithValue("@dvt", dvt);
                cmd.Parameters.AddWithValue("@sl", sl);
                cmd.Parameters.AddWithValue("@hinh", hinhanh);
                cmd.Parameters.AddWithValue("@mota", mota);
                cmd.Parameters.AddWithValue("@ncc", mancc);
                cmd.Parameters.AddWithValue("@dm", madm);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLSanPham");
        }

        // ==================== XÓA SẢN PHẨM ====================
        [HttpPost]
        public ActionResult XoaSanPham(string ma)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "DELETE FROM SANPHAM WHERE MaSP = @ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLSanPham");
        }

        // ==================== CẬP NHẬT SẢN PHẨM ====================
        [HttpPost]
        public ActionResult CapNhatSanPham(string ma, string ten, decimal dongia, int sl)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE SANPHAM SET TenSP = @ten, DonGia = @gia, SoLuongTon = @sl WHERE MaSP = @ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);
                cmd.Parameters.AddWithValue("@ten", ten);
                cmd.Parameters.AddWithValue("@gia", dongia);
                cmd.Parameters.AddWithValue("@sl", sl);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction("QLSanPham");
        }

        // ========== DANH SÁCH PHIẾU NHẬP ==========
        public ActionResult QLNhapHang()
        {
            List<HOADONNHAPHANG> dsPhieu = new List<HOADONNHAPHANG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaHDN, NgayNhap, TongTien, MaNCC, MaQL FROM HOADONNHAPHANG ORDER BY NgayNhap DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    dsPhieu.Add(new HOADONNHAPHANG
                    {
                        MaHDN = rd["MaHDN"].ToString(),
                        NgayNhap = rd["NgayNhap"] != DBNull.Value ? Convert.ToDateTime(rd["NgayNhap"]) : DateTime.MinValue,
                        TongTien = rd["TongTien"] != DBNull.Value ? Convert.ToDecimal(rd["TongTien"]) : 0,
                        MaNCC = rd["MaNCC"].ToString(),
                        MaQL = rd["MaQL"].ToString()
                    });
                }

            }

            return View(dsPhieu);
        }

        // ========== TẠO PHIẾU NHẬP ==========
        [HttpPost]
        public ActionResult TaoPhieuNhap(string mancc)
        {
            string maQL = Session["UserID"]?.ToString();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "INSERT INTO HOADONNHAPHANG (NgayNhap, TongTien, MaNCC, MaQL) VALUES (GETDATE(), 0, @ncc, @ql)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ncc", mancc);
                cmd.Parameters.AddWithValue("@ql", maQL);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Tạo phiếu nhập mới thành công!";
            return RedirectToAction("QLNhapHang");
        }

        // ========== XEM CHI TIẾT PHIẾU NHẬP ==========
        public ActionResult ChiTietPhieuNhap(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("QLNhapHang");

            id = id.Trim(); // Xử lý khoảng trắng

            List<PHIEUNHAP> dsCT = new List<PHIEUNHAP>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM PHIEUNHAP WHERE MaHDN = @id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    dsCT.Add(new PHIEUNHAP
                    {
                        MaHDN = rd["MaHDN"].ToString(),
                        MaSP = rd["MaSP"].ToString(),
                        SoLuong = Convert.ToInt32(rd["SoLuong"]),
                        DonGiaNhap = Convert.ToDecimal(rd["DonGiaNhap"])
                    });
                }
            }
            ViewBag.MaHDN = id;
            return View(dsCT);
        }


        // ========== THÊM SẢN PHẨM VÀO PHIẾU NHẬP ==========
        [HttpPost]
        public ActionResult ThemChiTiet(string mahdn, string masp, int sl, decimal gianhap)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "INSERT INTO PHIEUNHAP (MaHDN, MaSP, SoLuong, DonGiaNhap) VALUES (@hdn, @sp, @sl, @gia)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@hdn", mahdn);
                cmd.Parameters.AddWithValue("@sp", masp);
                cmd.Parameters.AddWithValue("@sl", sl);
                cmd.Parameters.AddWithValue("@gia", gianhap);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Đã thêm sản phẩm vào phiếu nhập!";
            return RedirectToAction("ChiTietPhieuNhap", new { id = mahdn });
        }

        // ========== CẬP NHẬT TỔNG TIỀN PHIẾU NHẬP ==========
        public ActionResult CapNhatTongTien(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("QLNhapHang");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Tạo transaction để đảm bảo an toàn dữ liệu
                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    // 1️⃣ Cập nhật tổng tiền phiếu nhập
                    string sqlTongTien = @"
                UPDATE HOADONNHAPHANG
                SET TongTien = (
                    SELECT ISNULL(SUM(SoLuong * DonGiaNhap), 0)
                    FROM PHIEUNHAP
                    WHERE PHIEUNHAP.MaHDN = HOADONNHAPHANG.MaHDN
                )
                WHERE MaHDN = @id";

                    SqlCommand cmdTong = new SqlCommand(sqlTongTien, conn, tran);
                    cmdTong.Parameters.AddWithValue("@id", id);
                    cmdTong.ExecuteNonQuery();

                    // 2️⃣ Cập nhật số lượng tồn kho (cộng thêm số lượng nhập)
                    string sqlUpdateStock = @"
                UPDATE S
                SET S.SoLuongTon = S.SoLuongTon + P.SoLuong
                FROM SANPHAM S
                INNER JOIN PHIEUNHAP P ON S.MaSP = P.MaSP
                WHERE P.MaHDN = @id";

                    SqlCommand cmdStock = new SqlCommand(sqlUpdateStock, conn, tran);
                    cmdStock.Parameters.AddWithValue("@id", id);
                    cmdStock.ExecuteNonQuery();

                    // Xác nhận transaction
                    tran.Commit();

                    TempData["ThongBao"] = "✅ Đã cập nhật tổng tiền và tồn kho thành công!";
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    TempData["Loi"] = "❌ Lỗi khi cập nhật: " + ex.Message;
                }
            }

            return RedirectToAction("ChiTietPhieuNhap", new { id = id });
        }


        // =============================
        // HIỂN THỊ QUẢN LÝ TỒN KHO
        // =============================
        public ActionResult QLTonKho()
        {
            List<SANPHAM> dsSP = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaSP, TenSP, DonViTinh, SoLuongTon FROM SANPHAM";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    dsSP.Add(new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonViTinh = rd["DonViTinh"].ToString(),
                        SoLuongTon = Convert.ToInt32(rd["SoLuongTon"])
                    });
                }
            }

            return View("QLTonKho", dsSP);
        }


        // ===============================
        // BÁO CÁO DOANH THU
        // ===============================
        public ActionResult BaoCaoDoanhThu()
        {
            List<BaoCaoDoanhThu> dsBC = new List<BaoCaoDoanhThu>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
            SELECT 
                CONVERT(DATE, NgayLap) AS Ngay,
                SUM(TongTien) AS TongBan,
                ISNULL((
                    SELECT SUM(TongTien) 
                    FROM HOADONNHAPHANG HN 
                    WHERE CONVERT(DATE, HN.NgayNhap) = CONVERT(DATE, HD.NgayLap)
                ), 0) AS TongNhap
            FROM HOADON HD
            WHERE TrangThai = N'Đã thanh toán'
            GROUP BY CONVERT(DATE, NgayLap)
            ORDER BY NgayLap DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    dsBC.Add(new BaoCaoDoanhThu
                    {
                        Ngay = Convert.ToDateTime(rd["Ngay"]),
                        TongBan = Convert.ToDecimal(rd["TongBan"]),
                        TongNhap = Convert.ToDecimal(rd["TongNhap"]),
                        LoiNhuan = Convert.ToDecimal(rd["TongBan"]) - Convert.ToDecimal(rd["TongNhap"])
                    });
                }
            }

            return View(dsBC);
        }

        public ActionResult QLDonHang()
        {
            List<HOADON> ds = new List<HOADON>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT MaHD, NgayLap, TongTien, TrangThai, PhuongThucTT, MaKH
                       FROM HOADON
                       ORDER BY NgayLap DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    ds.Add(new HOADON
                    {
                        MaHD = rd["MaHD"].ToString(),
                        NgayLap = Convert.ToDateTime(rd["NgayLap"]),
                        TongTien = Convert.ToDecimal(rd["TongTien"]),
                        TrangThai = rd["TrangThai"].ToString(),
                        PhuongThucTT = rd["PhuongThucTT"].ToString(),
                        MaKH = rd["MaKH"].ToString()
                    });
                }
            }

            return View(ds);
        }


    }
}
