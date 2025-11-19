using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    [KiemTraQuyen]
    public class QuanLyController : Controller
    {
        private readonly string connStr = ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // ================== TRANG CHÍNH QUẢN LÝ ==================
        public ActionResult Index()
        {
            if (Session["Role"] == null || Session["Role"].ToString() != "quanly")
                return RedirectToAction("Login", "Account");

            ViewBag.IsManager = true;
            return View();
        }

        // ======================= QUẢN LÝ SẢN PHẨM =======================
        public ActionResult QLSanPham(string keyword, string tonkho)
        {
            List<SANPHAM> sanPhams = new List<SANPHAM>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT MaSP, TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM, TrangThai
                    FROM SANPHAM
                    WHERE 
                        (@kw IS NULL OR MaSP LIKE '%' + @kw + '%' OR TenSP LIKE '%' + @kw + '%')
                        AND
                        (
                            @tk = 'all' 
                            OR (@tk = 'instock' AND SoLuongTon > 0)
                            OR (@tk = 'outstock' AND SoLuongTon = 0)
                        )
                    ORDER BY SoLuongTon DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@kw", string.IsNullOrEmpty(keyword) ? (object)DBNull.Value : keyword);
                cmd.Parameters.AddWithValue("@tk", string.IsNullOrEmpty(tonkho) ? "all" : tonkho);

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
                        MaDM = rd["MaDM"].ToString(),
                        TrangThai = rd["TrangThai"] == DBNull.Value ? "" : rd["TrangThai"].ToString()
                    });
                }
            }

            ViewBag.Keyword = keyword;
            ViewBag.TonKho = tonkho;
            // Lấy danh sách nhà cung cấp
            var listNCC = new List<NHACUNGCAP>();
            using (var conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaNCC, TenNCC FROM NHACUNGCAP";
                var cmd = new SqlCommand(sql, conn);
                conn.Open();
                var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    listNCC.Add(new NHACUNGCAP
                    {
                        MaNCC = rd["MaNCC"].ToString(),
                        TenNCC = rd["TenNCC"].ToString(),
                    });
                }
            }
            ViewBag.NCC = listNCC;


            var listDM = new List<DANHMUC>();
            using (var conn = new SqlConnection(connStr))
            {
                string sql = "SELECT MaDM, TenDM FROM DANHMUC";
                var cmd = new SqlCommand(sql, conn);
                conn.Open();
                var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    listDM.Add(new DANHMUC
                    {
                        MaDM = rd["MaDM"].ToString(),
                        TenDM = rd["TenDM"].ToString(),
                    });
                }
            }
            ViewBag.DM = listDM;

            return View(sanPhams);
        }

        // ==================== THÊM SẢN PHẨM ====================
        [HttpPost]
        public ActionResult ThemSanPham(
    string ten,
    decimal dongia,
    string dvt,
    int sl,
    string mota,
    string mancc,
    string madm,
    HttpPostedFileBase hinhanh)
        {
            // ====== KIỂM TRA THIẾU DỮ LIỆU ======
            if (string.IsNullOrWhiteSpace(ten) ||
                string.IsNullOrWhiteSpace(dvt) ||
                string.IsNullOrWhiteSpace(mancc) ||
                string.IsNullOrWhiteSpace(madm) ||
                hinhanh == null || hinhanh.ContentLength == 0)
            {
                TempData["ThongBao"] = " Vui lòng nhập đầy đủ thông tin và chọn hình ảnh sản phẩm!";
                return RedirectToAction("QLSanPham");
            }

            string fileName = System.IO.Path.GetFileName(hinhanh.FileName);
            string path = Server.MapPath("~/imagesSanPham/" + fileName);
            hinhanh.SaveAs(path);


            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO SANPHAM 
                        (TenSP, DonGia, DonViTinh, SoLuongTon, HinhAnh, MoTa, MaNCC, MaDM) 
                       VALUES 
                        (@ten, @gia, @dvt, @sl, @hinh, @mota, @ncc, @dm)";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@ten", ten);
                cmd.Parameters.AddWithValue("@gia", dongia);
                cmd.Parameters.AddWithValue("@dvt", dvt);
                cmd.Parameters.AddWithValue("@sl", sl);
                cmd.Parameters.AddWithValue("@hinh", "imagesSanPham/" + fileName);
                cmd.Parameters.AddWithValue("@mota", mota ?? "");
                cmd.Parameters.AddWithValue("@ncc", mancc);
                cmd.Parameters.AddWithValue("@dm", madm);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = " Thêm sản phẩm thành công!";
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
                string sql = @"UPDATE SANPHAM 
                               SET TenSP = @ten, DonGia = @gia, SoLuongTon = @sl 
                               WHERE MaSP = @ma";

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

        // ==================== QUẢN LÝ PHIẾU NHẬP ====================
        public ActionResult QLNhapHang(string search)
        {
            List<HOADONNHAPHANG> dsPhieu = new List<HOADONNHAPHANG>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT MaHDN, NgayNhap, TongTien, MaNCC, MaQL 
                    FROM HOADONNHAPHANG
                    WHERE 
                        (@search IS NULL 
                         OR MaHDN LIKE '%' + @search + '%'
                         OR MaNCC LIKE '%' + @search + '%'
                         OR CONVERT(VARCHAR, NgayNhap, 105) LIKE '%' + @search + '%')
                    ORDER BY NgayNhap DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@search", string.IsNullOrEmpty(search) ? (object)DBNull.Value : search);

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

            ViewBag.Search = search;
            return View(dsPhieu);
        }

        // ==================== TẠO PHIẾU NHẬP ====================
        [HttpPost]
        public ActionResult TaoPhieuNhap(string mancc)
        {
            string maQL = Session["UserID"]?.ToString();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO HOADONNHAPHANG (NgayNhap, TongTien, MaNCC, MaQL) 
                               VALUES (GETDATE(), 0, @ncc, @ql)";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ncc", mancc);
                cmd.Parameters.AddWithValue("@ql", maQL);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["ThongBao"] = "Tạo phiếu nhập mới thành công!";
            return RedirectToAction("QLNhapHang");
        }

        // ==================== CHI TIẾT PHIẾU NHẬP ====================
        public ActionResult ChiTietPhieuNhap(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("QLNhapHang");

            id = id.Trim();
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

        // ==================== THÊM CHI TIẾT PHIẾU NHẬP ====================
        [HttpPost]
        public ActionResult ThemChiTiet(string mahdn, string masp, int sl, decimal gianhap)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"INSERT INTO PHIEUNHAP (MaHDN, MaSP, SoLuong, DonGiaNhap) 
                               VALUES (@hdn, @sp, @sl, @gia)";

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

        // ==================== CẬP NHẬT TỔNG TIỀN PHIẾU NHẬP ====================
        public ActionResult CapNhatTongTien(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("QLNhapHang");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    string sqlTong = @"
                        UPDATE HOADONNHAPHANG
                        SET TongTien = (
                            SELECT ISNULL(SUM(SoLuong * DonGiaNhap), 0)
                            FROM PHIEUNHAP WHERE MaHDN = @id
                        )
                        WHERE MaHDN = @id";

                    SqlCommand cmdTong = new SqlCommand(sqlTong, conn, tran);
                    cmdTong.Parameters.AddWithValue("@id", id);
                    cmdTong.ExecuteNonQuery();

                    string sqlStock = @"
                        UPDATE S
                        SET S.SoLuongTon = S.SoLuongTon + P.SoLuong
                        FROM SANPHAM S 
                        INNER JOIN PHIEUNHAP P ON S.MaSP = P.MaSP
                        WHERE P.MaHDN = @id";

                    SqlCommand cmdStock = new SqlCommand(sqlStock, conn, tran);
                    cmdStock.Parameters.AddWithValue("@id", id);
                    cmdStock.ExecuteNonQuery();

                    tran.Commit();
                    TempData["ThongBao"] = "Đã cập nhật tổng tiền & tồn kho!";
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    TempData["Loi"] = "Lỗi: " + ex.Message;
                }
            }

            return RedirectToAction("ChiTietPhieuNhap", new { id });
        }

        // ==================== QUẢN LÝ TỒN KHO ====================
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

            return View(dsSP);
        }

        // ==================== BÁO CÁO DOANH THU ====================
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
                        ),0) AS TongNhap
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

        // ==================== QUẢN LÝ ĐƠN HÀNG ====================
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

                var rd = cmd.ExecuteReader();
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

        // ==================== CHI TIẾT ĐƠN HÀNG ====================
        public ActionResult ChiTietDonHang(string id)
        {
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("QLDonHang");

            id = id.Trim();

            HOADON hoaDon = null;
            List<Dictionary<string, object>> chiTiet = new List<Dictionary<string, object>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                string sqlHD = @"
                    SELECT HD.MaHD, HD.NgayLap, HD.TongTien, HD.TrangThai, HD.PhuongThucTT,
                           ND.TenNguoiDung, ND.Email, ND.SDT, ND.DiaChi
                    FROM HOADON HD
                    JOIN NGUOIDUNG ND ON HD.MaKH = ND.MaUser
                    WHERE HD.MaHD = @id";

                SqlCommand cmdHD = new SqlCommand(sqlHD, conn);
                cmdHD.Parameters.AddWithValue("@id", id);

                var rdHD = cmdHD.ExecuteReader();
                if (rdHD.Read())
                {
                    hoaDon = new HOADON
                    {
                        MaHD = rdHD["MaHD"].ToString(),
                        NgayLap = Convert.ToDateTime(rdHD["NgayLap"]),
                        TongTien = Convert.ToDecimal(rdHD["TongTien"]),
                        TrangThai = rdHD["TrangThai"].ToString(),
                        PhuongThucTT = rdHD["PhuongThucTT"].ToString(),
                        TenKH = rdHD["TenNguoiDung"].ToString(),
                        Email = rdHD["Email"].ToString(),
                        SDT = rdHD["SDT"].ToString(),
                        DiaChi = rdHD["DiaChi"].ToString()
                    };
                }
                rdHD.Close();

                string sqlCT = @"
                    SELECT C.MaSP, S.TenSP, C.SoLuong, C.DonGia,
                           (C.SoLuong * C.DonGia) AS ThanhTien
                    FROM CTHOADON C
                    JOIN SANPHAM S ON C.MaSP = S.MaSP
                    WHERE C.MaHD = @MaHD";

                SqlCommand cmdCT = new SqlCommand(sqlCT, conn);
                cmdCT.Parameters.AddWithValue("@MaHD", id);

                var rdCT = cmdCT.ExecuteReader();
                while (rdCT.Read())
                {
                    chiTiet.Add(new Dictionary<string, object>
                    {
                        { "MaSP", rdCT["MaSP"].ToString() },
                        { "TenSP", rdCT["TenSP"].ToString() },
                        { "SoLuong", rdCT["SoLuong"] },
                        { "DonGia", Convert.ToDecimal(rdCT["DonGia"]).ToString("N0") + " đ" },
                        { "ThanhTien", Convert.ToDecimal(rdCT["ThanhTien"]).ToString("N0") + " đ" }
                    });
                }
            }

            ViewBag.HoaDon = hoaDon;
            return View(chiTiet);
        }

        // ==================== NGỪNG KINH DOANH ====================
        [HttpPost]
        public ActionResult NgungKinhDoanh(string ma)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string sql = "UPDATE SANPHAM SET TrangThai = N'NgungKinhDoanh' WHERE MaSP = @ma";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ma", ma.Trim());

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                TempData["ThongBao"] = "Đã ngừng kinh doanh sản phẩm!";
            }
            catch (Exception ex)
            {
                TempData["ThongBao"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("QLSanPham");
        }

        // ==================== MỞ BÁN LẠI ====================
        [HttpPost]
        public ActionResult KinhDoanhLai(string ma)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string sql = "UPDATE SANPHAM SET TrangThai = NULL WHERE MaSP = @ma";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ma", ma.Trim());

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                TempData["ThongBao"] = "Đã mở bán lại sản phẩm!";
            }
            catch (Exception ex)
            {
                TempData["ThongBao"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("QLSanPham");
        }
    }
}
