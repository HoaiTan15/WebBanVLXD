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
    // Chỉ admin mới được truy cập controller này
    [KiemTraQuyen]
    public class AdminController : Controller
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // Kiểm tra quyền admin
        private bool IsAdmin()
        {
            return Session["Role"] != null && Session["Role"].ToString() == "admin";
        }

        // =======================
        // DASHBOARD ADMIN
        // =======================
        public ActionResult Index()
        {
            return View();
        }

        // =======================
        // QUẢN LÝ TÀI KHOẢN
        // =======================
        public ActionResult QLTaiKhoan()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var list = new List<NGUOIDUNG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT MaUser, TenNguoiDung, Email, SDT, DiaChi, Role, TrangThai, NgayTao 
                               FROM NGUOIDUNG";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                var rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    list.Add(new NGUOIDUNG
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

            return View(list);
        }

        [HttpPost]
        public ActionResult ThemTaiKhoan(string TenNguoiDung, string Email, string MatKhau,
                                         string SDT, string DiaChi, string Role)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO NGUOIDUNG 
                               (TenNguoiDung, MatKhau, Email, SDT, DiaChi, Role, TrangThai, NgayTao) 
                               VALUES 
                               (@ten, @mk, @em, @sdt, @dc, @role, 'HoatDong', GETDATE())";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ten", TenNguoiDung);
                cmd.Parameters.AddWithValue("@mk", MatKhau);
                cmd.Parameters.AddWithValue("@em", Email);
                cmd.Parameters.AddWithValue("@sdt", SDT);
                cmd.Parameters.AddWithValue("@dc", DiaChi);
                cmd.Parameters.AddWithValue("@role", Role);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLTaiKhoan");
        }

        [HttpGet]
        public ActionResult XoaTaiKhoan(string ma)
        {
            if (string.IsNullOrWhiteSpace(ma))
                return RedirectToAction("QLTaiKhoan");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "DELETE FROM NGUOIDUNG WHERE MaUser=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma.Trim());

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLTaiKhoan");
        }

        [HttpPost]
        public ActionResult SuaTaiKhoan(NGUOIDUNG u)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"UPDATE NGUOIDUNG SET 
                               TenNguoiDung=@ten,
                               Email=@em,
                               SDT=@sdt,
                               DiaChi=@dc,
                               Role=@role
                               WHERE MaUser=@ma";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", u.MaUser);
                cmd.Parameters.AddWithValue("@ten", u.TenNguoiDung);
                cmd.Parameters.AddWithValue("@em", u.Email);
                cmd.Parameters.AddWithValue("@sdt",
                    string.IsNullOrWhiteSpace(u.SDT) ? (object)DBNull.Value : u.SDT);
                cmd.Parameters.AddWithValue("@dc",
                    string.IsNullOrWhiteSpace(u.DiaChi) ? (object)DBNull.Value : u.DiaChi);
                cmd.Parameters.AddWithValue("@role", u.Role);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLTaiKhoan");
        }

        // =======================
        // QUẢN LÝ SẢN PHẨM
        // =======================
        public ActionResult QLSanPham()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var list = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT SP.MaSP, SP.TenSP, SP.DonGia, SP.DonViTinh, SP.SoLuongTon,
                                      SP.HinhAnh, SP.MoTa, SP.MaNCC, SP.MaDM, SP.TrangThai,
                                      NCC.TenNCC, DM.TenDM
                               FROM SANPHAM SP
                               LEFT JOIN NHACUNGCAP NCC ON SP.MaNCC = NCC.MaNCC
                               LEFT JOIN DANHMUC DM ON SP.MaDM = DM.MaDM";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                var rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    list.Add(new SANPHAM
                    {
                        MaSP = rd["MaSP"].ToString(),
                        TenSP = rd["TenSP"].ToString(),
                        DonGia = Convert.ToDecimal(rd["DonGia"]),
                        DonViTinh = rd["DonViTinh"].ToString(),
                        SoLuongTon = Convert.ToInt32(rd["SoLuongTon"]),
                        HinhAnh = rd["HinhAnh"].ToString(),
                        MoTa = rd["MoTa"].ToString(),
                        MaNCC = rd["MaNCC"].ToString(),
                        MaDM = rd["MaDM"].ToString(),
                        TenNCC = rd["TenNCC"].ToString(),
                        TenDM = rd["TenDM"].ToString(),
                        TrangThai = rd["TrangThai"].ToString()
                    });
                }
            }

            return View(list);
        }

        public ActionResult SuaSanPham(string id)
        {
            SANPHAM sp = null;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT SP.*, NCC.TenNCC, DM.TenDM
                               FROM SANPHAM SP
                               LEFT JOIN NHACUNGCAP NCC ON SP.MaNCC = NCC.MaNCC
                               LEFT JOIN DANHMUC DM ON SP.MaDM = DM.MaDM
                               WHERE MaSP = @id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                conn.Open();
                var rd = cmd.ExecuteReader();

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

            ViewBag.NCC = GetNCC();
            ViewBag.DM = GetDanhMuc();

            return View(sp);
        }

        [HttpPost]
        public ActionResult SuaSanPham(SANPHAM sp, string HinhAnhCu, HttpPostedFileBase fileAnh)
        {
            string hinh = HinhAnhCu;

            if (fileAnh != null && fileAnh.ContentLength > 0)
            {
                string fileName = Path.GetFileName(fileAnh.FileName);
                string savePath = Path.Combine(Server.MapPath("~/imagesSanPham/"), fileName);
                fileAnh.SaveAs(savePath);

                hinh = "imagesSanPham/" + fileName;
            }

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"UPDATE SANPHAM SET 
                               TenSP=@ten, DonGia=@gia, DonViTinh=@dvt, SoLuongTon=@sl,
                               HinhAnh=@anh, MoTa=@mota, MaNCC=@ncc, MaDM=@dm
                               WHERE MaSP=@ma";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ma", sp.MaSP);
                cmd.Parameters.AddWithValue("@ten", sp.TenSP);
                cmd.Parameters.AddWithValue("@gia", sp.DonGia);
                cmd.Parameters.AddWithValue("@dvt", sp.DonViTinh);
                cmd.Parameters.AddWithValue("@sl", sp.SoLuongTon);
                cmd.Parameters.AddWithValue("@anh", hinh);
                cmd.Parameters.AddWithValue("@mota", sp.MoTa);
                cmd.Parameters.AddWithValue("@ncc", sp.MaNCC);
                cmd.Parameters.AddWithValue("@dm", sp.MaDM);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("QLSanPham");
        }

        [HttpPost]
        public ActionResult ThemSanPham(string ma, string ten, decimal dongia,
                                        string dvt, int sl, string hinhanh,
                                        string mota, string mancc, string madm)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO SANPHAM 
                               (MaSP, TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM) 
                               VALUES 
                               (@ma, @ten, @gia, @dvt, @sl, @hinh, @mota, @ncc, @dm)";

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
        public ActionResult NgungKinhDoanh(string ma)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE SANPHAM SET TrangThai='NgungKinhDoanh' WHERE MaSP=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Sản phẩm đã được ngừng bán!";
            return RedirectToAction("QLSanPham");
        }

        [HttpGet]
        public ActionResult KinhDoanhLai(string ma)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE SANPHAM SET TrangThai='HoatDong' WHERE MaSP=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", ma);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Sản phẩm đã mở bán lại!";
            return RedirectToAction("QLSanPham");
        }

        // =======================
        // PHÂN QUYỀN NGƯỜI DÙNG
        // =======================
        public ActionResult PhanQuyen()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            var list = new List<NGUOIDUNG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"SELECT MaUser, TenNguoiDung, Email, SDT, Role, TrangThai 
                               FROM NGUOIDUNG";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                var rd = cmd.ExecuteReader();

                while (rd.Read())
                {
                    list.Add(new NGUOIDUNG
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

            return View(list);
        }

        [HttpPost]
        public ActionResult CapNhatQuyen(string maUser, string role)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE NGUOIDUNG SET Role=@role WHERE MaUser=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@ma", maUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Cập nhật quyền thành công!";
            return RedirectToAction("PhanQuyen");
        }

        [HttpPost]
        public ActionResult KhoaUser(string maUser)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE NGUOIDUNG SET TrangThai='Khoa' WHERE MaUser=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", maUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Đã khóa tài khoản!";
            return RedirectToAction("PhanQuyen");
        }

        [HttpPost]
        public ActionResult MoKhoaUser(string maUser)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "UPDATE NGUOIDUNG SET TrangThai='HoatDong' WHERE MaUser=@ma";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ma", maUser);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Đã mở khóa tài khoản!";
            return RedirectToAction("PhanQuyen");
        }

        // =======================
        // THỐNG KÊ HỆ THỐNG
        // =======================
        public ActionResult ThongKe()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                ViewBag.TongSanPham = (int)new SqlCommand("SELECT COUNT(*) FROM SANPHAM", conn).ExecuteScalar();
                ViewBag.TongNguoiDung = (int)new SqlCommand("SELECT COUNT(*) FROM NGUOIDUNG", conn).ExecuteScalar();
                ViewBag.TongDonHang = (int)new SqlCommand("SELECT COUNT(*) FROM HOADON", conn).ExecuteScalar();

                decimal tongBan = Convert.ToDecimal(new SqlCommand(
                    "SELECT ISNULL(SUM(TongTien),0) FROM HOADON WHERE TrangThai=N'Đã thanh toán'", conn).ExecuteScalar());

                decimal tongNhap = Convert.ToDecimal(new SqlCommand(
                    "SELECT ISNULL(SUM(TongTien),0) FROM HOADONNHAPHANG", conn).ExecuteScalar());

                ViewBag.DoanhThu = tongBan - tongNhap;
            }

            return View();
        }

        // =======================
        // HÀM PHỤ LẤY DANH SÁCH NCC & DM
        // =======================
        private List<NHACUNGCAP> GetNCC()
        {
            var list = new List<NHACUNGCAP>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                var rd = new SqlCommand("SELECT MaNCC, TenNCC FROM NHACUNGCAP", conn).ExecuteReader();

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
                var rd = new SqlCommand("SELECT MaDM, TenDM FROM DANHMUC", conn).ExecuteReader();

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
