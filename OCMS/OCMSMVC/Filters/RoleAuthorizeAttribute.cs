using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OCMSMVC.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RoleAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] allowedRoles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            allowedRoles = roles;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userId = session.GetInt32("UserId");
            var userRole = session.GetString("Role");

            // Chưa đăng nhập
            if (!userId.HasValue || string.IsNullOrEmpty(userRole))
            {
                // Lưu lại URL hiện tại để sau khi login redirect về
                var request = context.HttpContext.Request;
                var returnUrl = request.Path + request.QueryString;

                // Set TempData thông qua TempDataDictionary
                var tempData = context.Controller is Controller ctrl ? ctrl.TempData : null;
                if (tempData != null)
                    tempData["ErrorMessage"] = "Vui lòng đăng nhập để tiếp tục truy cập trang này.";

                context.Result = new RedirectToActionResult("Login", "Auth", new { returnUrl = returnUrl.ToString() });
                return;
            }

            // Đã đăng nhập nhưng không đúng role
            if (allowedRoles.Length > 0 && !allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
            {
                var tempData = context.Controller is Controller ctrl ? ctrl.TempData : null;
                if (tempData != null)
                    tempData["ErrorMessage"] = "Bạn không có quyền truy cập vào chức năng này.";

                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}