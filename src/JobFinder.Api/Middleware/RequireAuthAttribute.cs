// ============================================================
// FILE: Middleware/RequireAuthAttribute.cs
// CONCEPT: Minimal authentication gate (no role check)
// ------------------------------------------------------------
// [RequireAuth] is a simpler sibling of [AuthorizeRoles].
// It only checks "is the user logged in?" — no role required.
//
// Use it on endpoints that any authenticated user can reach:
//   [RequireAuth]
//   public async Task<IActionResult> GetMe() { ... }
//
// vs
//   [AuthorizeRoles(Role.RECRUITER)]
//   public async Task<IActionResult> CreateJob() { ... }
// ============================================================

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JobFinder.Api.Middleware
{
    // 📌 CONCEPT: Attribute without constructor arguments
    //    Usage:  [RequireAuth]
    //    No roles to pass — just checks for a valid JWT.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireAuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // 📌 FindFirst("userId") returns null if the claim doesn't exist
            //    (i.e. the user is not authenticated or the JWT was invalid).
            var userId = user.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                // Pull the specific error message set by AuthMiddleware.
                var authError = context.HttpContext.Items["AuthError"] as string ?? "Missing access token";

                context.Result = new JsonResult(new { error = authError })
                {
                    StatusCode = 401
                };
            }

            // If userId is present, the action runs normally.
            return Task.CompletedTask;
        }
    }
}
