using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanVLXD.Models;

namespace WebBanVLXD.Controllers
{
    public class SanPhamController : Controller
    {
       
        VLXD_DBDataContext db = new VLXD_DBDataContext(System.Configuration.ConfigurationManager.ConnectionStrings["VLXD_DBConnectionString"].ConnectionString);

        // GET: SanPham
        public ActionResult Index()
        {
            // Lấy danh sách sản phẩm
            var sanPhams = from sp in db.SANPHAMs
                           select sp;

            return View(sanPhams.ToList());
        }

        // Xem chi tiết sản phẩm
        public ActionResult ChiTiet(string id)
        {
            if (string.IsNullOrEmpty(id)) return HttpNotFound();
            var sp = db.SANPHAMs.FirstOrDefault(s => s.MaSP == id);
            if (sp == null) return HttpNotFound();

            return View(sp);
        }
    }
}
