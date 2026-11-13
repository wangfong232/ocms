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

            if (!userId.HasValue || string.IsNullOrEmpty(userRole))
            {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            if (allowedRoles.Length > 0 && !allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }

    }
}
