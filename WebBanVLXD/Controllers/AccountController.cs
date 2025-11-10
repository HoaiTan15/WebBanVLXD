using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        VLXD_DBEntities db = new VLXD_DBEntities();
        public ActionResult Login()
        {

            return View();
        }
        [HttpPost]
        public ActionResult Login(string TenDangNhap, string MatKhau)
        {
            var user = db.TaiKhoans.FirstOrDefault(u => u.TenDangNhap == TenDangNhap && u.MatKhau == MatKhau && u.TrangThai == true);
            if (user != null)
            {
                // Lưu session đăng nhập
                Session["UserID"] = user.TaiKhoanID;
                Session["UserName"] = user.TenDangNhap;
                Session["Role"] = user.PhanQuyen.TenQuyen;

                // Chuyển hướng theo quyền
                if (user.PhanQuyen.TenQuyen == "Admin")
                    return RedirectToAction("TrangChu", "Home");
                else if (user.PhanQuyen.TenQuyen == "NhanVien")
                    return RedirectToAction("Index", "SanPham");
                else
                    return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
            return View();
        }
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }
        
    }
}