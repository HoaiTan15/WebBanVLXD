using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebBanVLXD.Models
{
    public class HOADON
    {
        public string MaHD { get; set; }
        public DateTime NgayLap { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public string PhuongThucTT { get; set; }
        public string MaKH { get; set; }
        public string MaQL { get; set; }


        // ===== Thêm 4 cái này để hiển thị cho quản lý =====
        public string TenKH { get; set; }
        public string Email { get; set; }
        public string SDT { get; set; }
        public string DiaChi { get; set; }
    }
}