using System.Web;
using System.Web.Mvc;

namespace WebBanVLXD.Models
{
    public class KiemTraQuyen : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Nếu đang vào trang Login hoặc Register thì cho phép
            string path = httpContext.Request.Path.ToLower();
            if (path.Contains("/account/login") || path.Contains("/account/register"))
                return true;

            var role = httpContext.Session["Role"]?.ToString();

            // Chỉ admin hoặc quản lý được truy cập
            return role == "admin" || role == "quanly";
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // Điều hướng đến trang Login
            filterContext.Result = new RedirectResult("/Account/Login");
        }
    }
}
