using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Middleware
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeRolesAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _roles;

        public AuthorizeRolesAttribute(params Role[] roles)
        {
            _roles = roles.Select(r => r.ToString()).ToArray();
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            var userId = user.FindFirst("userId")?.Value;

            // 1. Check if authenticated
            if (string.IsNullOrEmpty(userId))
            {
                var authError = context.HttpContext.Items["AuthError"] as string ?? "Missing access token";
                context.Result = new JsonResult(new { error = authError })
                {
                    StatusCode = 401
                };
                return Task.CompletedTask;
            }

            // 2. Check roles by directly reading the "roles" claim values.
            // We avoid user.IsInRole() because JwtSecurityTokenHandler can remap
            // custom claim names to full URI equivalents during token validation,
            // causing IsInRole() to silently fail even when the claim is present.
            var userRoles = user.FindAll("roles").Select(c => c.Value).ToHashSet();
            var hasRole = _roles.Any(role => userRoles.Contains(role));
            if (!hasRole)
            {
                context.Result = new JsonResult(new { error = "Access denied." })
                {
                    StatusCode = 403
                };
            }

            return Task.CompletedTask;
        }
    }
}
