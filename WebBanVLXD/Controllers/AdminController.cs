using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class AdminController : Controller
    {
        private bool IsAdmin()
        {
            return Session["Role"] != null && Session["Role"].ToString() == "admin";
        }

        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // ========== DASHBOARD ADMIN ==========
        public ActionResult Index()
        {
            return View();
        }

        // =====================================================
        // ========== QUẢN LÝ TÀI KHOẢN =========================
        // =====================================================
        public ActionResult QLTaiKhoan()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            List<NGUOIDUNG> nguoiDungs = new List<NGUOIDUNG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaUser, TenNguoiDung, Email, SDT, DiaChi, Role, TrangThai, NgayTao FROM NGUOIDUNG";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    nguoiDungs.Add(new NGUOIDUNG
                    {
                        MaUser = rd["MaUser"].ToString(),
                        TenNguoiDung = rd["TenNguoiDung"].ToString(),
                        Email = rd["Email"].ToString(),
                        SDT = rd["SDT"].ToString(),
                        DiaChi = rd["DiaChi"].ToString(),
                        Role = rd["Role"].ToString(),
                        TrangThai = rd["TrangThai"].ToString(),
                        NgayTao = Convert.ToDateTime(rd["NgayTao"])
                    });
                }
            }

            return View(nguoiDungs);
        }

        [HttpPost]
        public ActionResult ThemTaiKhoan(string ma, string ten, string email, string matkhau, string sdt, string diachi, string role)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "INSERT INTO NGUOIDUNG (MaUser, TenNguoiDung, MatKhau, Email, SDT, DiaChi, Role, TrangThai, NgayTao) " +
                             "VALUES (@ma, @ten, @matkhau, @em, @sdt, @dc, @role, 'HoatDong', GETDATE())";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);
                cmd.Parameters.AddWithValue("@ten", ten);
                cmd.Parameters.AddWithValue("@matkhau", matkhau);
                cmd.Parameters.AddWithValue("@em", email);
                cmd.Parameters.AddWithValue("@sdt", sdt);
                cmd.Parameters.AddWithValue("@dc", diachi);
                cmd.Parameters.AddWithValue("@role", role);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("QLTaiKhoan");
        }

        [HttpPost]
        public ActionResult XoaTaiKhoan(string ma)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "DELETE FROM NGUOIDUNG WHERE MaUser = @ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("QLTaiKhoan");
        }


        // =====================================================
        // ========== QUẢN LÝ SẢN PHẨM =========================
        // =====================================================
        public ActionResult QLSanPham()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            List<SANPHAM> sanPhams = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
SELECT 
    SP.MaSP, SP.TenSP, SP.DonGia, SP.DonViTinh, SP.SoLuongTon, 
    SP.HinhAnh, SP.MoTa,
    SP.MaNCC, NCC.TenNCC,
    SP.MaDM, DM.TenDM
FROM SANPHAM SP
LEFT JOIN NHACUNGCAP NCC ON SP.MaNCC = NCC.MaNCC
LEFT JOIN DANHMUC DM ON SP.MaDM = DM.MaDM
";

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
                        TenNCC = rd["TenNCC"].ToString(),
                        MaDM = rd["MaDM"].ToString(),
                        TenDM = rd["TenDM"].ToString()
                    });
                }
            }

            return View(sanPhams);
        }
        public ActionResult SuaSanPham(string id)
        {
            SANPHAM sp = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
        SELECT SP.*, NCC.TenNCC, DM.TenDM
        FROM SANPHAM SP
        LEFT JOIN NHACUNGCAP NCC ON SP.MaNCC = NCC.MaNCC
        LEFT JOIN DANHMUC DM ON SP.MaDM = DM.MaDM
        WHERE MaSP = @id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();

                if (rd.Read())
                {
                    sp = new SANPHAM
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
                    };
                }
            }

            // Lấy danh sách NCC + DM để show dropdown
            ViewBag.NCC = GetNCC();
            ViewBag.DM = GetDanhMuc();

            return View(sp);
        }

        [HttpPost]
        public ActionResult SuaSanPham(SANPHAM sp, string HinhAnhCu, HttpPostedFileBase fileAnh)
        {
            string hinhAnhMoi = HinhAnhCu; // mặc định giữ ảnh cũ

            // Nếu người dùng chọn file ảnh mới
            if (fileAnh != null && fileAnh.ContentLength > 0)
            {
                string fileName = Path.GetFileName(fileAnh.FileName);
                string savePath = Path.Combine(Server.MapPath("~/imagesSanPham/"), fileName);

                fileAnh.SaveAs(savePath); // Lưu file lên server

                hinhAnhMoi = "imagesSanPham/" + fileName;
            }

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
UPDATE SANPHAM SET 
    TenSP = @ten,
    DonGia = @gia,
    DonViTinh = @dvt,
    SoLuongTon = @ton,
    HinhAnh = @anh,
    MoTa = @mota,
    MaNCC = @ncc,
    MaDM = @dm
WHERE MaSP = @ma";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ma", sp.MaSP);
                cmd.Parameters.AddWithValue("@ten", sp.TenSP);
                cmd.Parameters.AddWithValue("@gia", sp.DonGia);
                cmd.Parameters.AddWithValue("@dvt", sp.DonViTinh);
                cmd.Parameters.AddWithValue("@ton", sp.SoLuongTon);
                cmd.Parameters.AddWithValue("@anh", hinhAnhMoi);   // ✔ ảnh mới hoặc cũ
                cmd.Parameters.AddWithValue("@mota", sp.MoTa);
                cmd.Parameters.AddWithValue("@ncc", sp.MaNCC);
                cmd.Parameters.AddWithValue("@dm", sp.MaDM);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLSanPham");
        }



        [HttpPost]
        public ActionResult ThemSanPham(string ma, string ten, decimal dongia, string dvt, int sl, string hinhanh, string mota, string mancc, string madm)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "INSERT INTO SANPHAM (MaSP, TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM) " +
                             "VALUES (@ma, @ten, @gia, @dvt, @sl, @hinh, @mota, @ncc, @dm)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);
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

        [HttpPost]
        public ActionResult XoaSanPham(string ma)
        {
                if (!IsAdmin())
        return RedirectToAction("Login", "Account");

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


        // =====================================================
        // ========== PHÂN QUYỀN NGƯỜI DÙNG ====================
        // =====================================================
        public ActionResult PhanQuyen()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            List<NGUOIDUNG> users = new List<NGUOIDUNG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaUser, TenNguoiDung, Email, SDT, Role, TrangThai FROM NGUOIDUNG";
                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SqlDataReader rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    users.Add(new NGUOIDUNG
                    {
                        MaUser = rd["MaUser"].ToString(),
                        TenNguoiDung = rd["TenNguoiDung"].ToString(),
                        Email = rd["Email"].ToString(),
                        SDT = rd["SDT"].ToString(),
                        Role = rd["Role"].ToString(),
                        TrangThai = rd["TrangThai"].ToString()
                    });
                }
            }

            return View(users);
        }

        [HttpPost]
        public ActionResult CapNhatQuyen(string maUser, string role)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE NGUOIDUNG SET Role = @role WHERE MaUser = @ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@ma", maUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Cập nhật quyền thành công!";
            return RedirectToAction("PhanQuyen");
        }


        // =====================================================
        // ========== THỐNG KÊ HỆ THỐNG ========================
        // =====================================================
        public ActionResult ThongKe()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Tổng sản phẩm
                SqlCommand cmd1 = new SqlCommand("SELECT COUNT(*) FROM SANPHAM", conn);
                ViewBag.TongSanPham = (int)cmd1.ExecuteScalar();

                // Tổng người dùng
                SqlCommand cmd2 = new SqlCommand("SELECT COUNT(*) FROM NGUOIDUNG", conn);
                ViewBag.TongNguoiDung = (int)cmd2.ExecuteScalar();

                // Tổng đơn hàng
                SqlCommand cmd3 = new SqlCommand("SELECT COUNT(*) FROM HOADON", conn);
                ViewBag.TongDonHang = (int)cmd3.ExecuteScalar();

                // Doanh thu bán hàng
                SqlCommand cmdBan = new SqlCommand("SELECT ISNULL(SUM(TongTien),0) FROM HOADON WHERE TrangThai = N'Đã thanh toán'", conn);
                decimal tongBan = Convert.ToDecimal(cmdBan.ExecuteScalar());

                // Chi phí nhập hàng
                SqlCommand cmdNhap = new SqlCommand("SELECT ISNULL(SUM(TongTien),0) FROM HOADONNHAPHANG", conn);
                decimal tongNhap = Convert.ToDecimal(cmdNhap.ExecuteScalar());

                // Doanh thu ròng (lợi nhuận)
                ViewBag.DoanhThu = tongBan - tongNhap;
            }

            return View();
        }
        private List<NHACUNGCAP> GetNCC()
        {
            var list = new List<NHACUNGCAP>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT MaNCC, TenNCC FROM NHACUNGCAP", conn);
                var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    list.Add(new NHACUNGCAP
                    {
                        MaNCC = rd["MaNCC"].ToString(),
                        TenNCC = rd["TenNCC"].ToString()
                    });
                }
            }
            return list;
        }

        private List<DANHMUC> GetDanhMuc()
        {
            var list = new List<DANHMUC>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT MaDM, TenDM FROM DANHMUC", conn);
                var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    list.Add(new DANHMUC
                    {
                        MaDM = rd["MaDM"].ToString(),
                        TenDM = rd["TenDM"].ToString()
                    });
                }
            }
            return list;
        }

    }
}
