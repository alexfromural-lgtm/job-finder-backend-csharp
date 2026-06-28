using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JobFinder.Api.Middleware
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireAuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            var userId = user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                var authError = context.HttpContext.Items["AuthError"] as string ?? "Missing access token";
                context.Result = new JsonResult(new { error = authError })
                {
                    StatusCode = 401
                };
            }

            return Task.CompletedTask;
        }
    }
}
