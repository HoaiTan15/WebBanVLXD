using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class SanPhamController : Controller
    {
        private readonly string connStr =
            ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString;

        // ----------------- DANH SÁCH SẢN PHẨM -----------------
        public ActionResult Index(string keyword, string madm, string sort)
        {
            var sanPhams = new List<SANPHAM>();
            var danhMucs = new List<DANHMUC>();

            // Lấy danh mục
            using (var conn = new SqlConnection(connStr))
            {
                string sqlDM = "SELECT * FROM DANHMUC";
                var cmdDM = new SqlCommand(sqlDM, conn);

                conn.Open();
                var rdDM = cmdDM.ExecuteReader();
                while (rdDM.Read())
                {
                    danhMucs.Add(new DANHMUC
                    {
                        MaDM = rdDM["MaDM"].ToString(),
                        TenDM = rdDM["TenDM"].ToString(),
                        HinhAnh = rdDM["HinhAnh"].ToString()
                    });
                }
            }

            // Lấy sản phẩm
            using (var conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM SANPHAM WHERE 1=1";

                if (!string.IsNullOrEmpty(keyword))
                    sql += " AND TenSP LIKE @keyword";

                if (!string.IsNullOrEmpty(madm))
                    sql += " AND MaDM = @madm";

                if (!string.IsNullOrEmpty(sort))
                {
                    sql += sort == "asc"
                        ? " ORDER BY DonGia ASC"
                        : " ORDER BY DonGia DESC";
                }

                var cmd = new SqlCommand(sql, conn);

                if (!string.IsNullOrEmpty(keyword))
                    cmd.Parameters.AddWithValue("@keyword", "%" + keyword + "%");

                if (!string.IsNullOrEmpty(madm))
                    cmd.Parameters.AddWithValue("@madm", madm);

                conn.Open();
                var rd = cmd.ExecuteReader();

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
                        TrangThai = rd["TrangThai"].ToString()


                    });
                }
            }

            ViewBag.DanhMuc = danhMucs;
            ViewBag.IsAdmin = (Session["Role"] != null &&
                               Session["Role"].ToString() == "admin");

            return View(sanPhams);
        }

        // ----------------- CHI TIẾT SẢN PHẨM -----------------
        public ActionResult ChiTiet(string id)
        {
            if (string.IsNullOrEmpty(id))
                return HttpNotFound();

            id = id.Trim();
            SANPHAM sp = null;
            string tenDanhMuc = "";

            // Lấy thông tin sản phẩm
            using (var conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM SANPHAM WHERE MaSP=@MaSP";
                var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MaSP", id);
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
                        MaDM = rd["MaDM"].ToString(),
                        TrangThai = rd["TrangThai"].ToString()

                    };
                }
            }

            if (sp == null)
                return HttpNotFound();

            // Lấy tên danh mục
            using (var conn = new SqlConnection(connStr))
            {
                string sql = "SELECT TenDM FROM DANHMUC WHERE MaDM=@MaDM";
                var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@MaDM", sp.MaDM);

                conn.Open();
                var result = cmd.ExecuteScalar();

                if (result != null)
                    tenDanhMuc = result.ToString();
            }

            ViewBag.TenDanhMuc = tenDanhMuc;
            ViewBag.MaDanhMuc = sp.MaDM;

            return View(sp);
        }
    }
}
