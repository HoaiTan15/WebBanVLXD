using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebBanVLXD.Models
{
    public class NGUOIDUNG
    {
        public string MaUser { get; set; }
        public string TenNguoiDung { get; set; }
        public string MatKhau { get; set; }
        public string Email { get; set; }
        public string SDT { get; set; }
        public string DiaChi { get; set; }
        public string Role { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
    }
}