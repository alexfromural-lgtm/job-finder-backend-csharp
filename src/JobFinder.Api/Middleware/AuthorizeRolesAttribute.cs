// ============================================================
// FILE: Middleware/AuthorizeRolesAttribute.cs
// CONCEPT: Custom Authorization Filter Attribute
// ------------------------------------------------------------
// ASP.NET Core's built-in [Authorize] works with its own
// authentication scheme.  We use cookie-based JWT (not the
// built-in Identity), so we write our own attribute that:
//   1. Checks the user IS authenticated (has a valid JWT).
//   2. Checks the user HAS at least one of the required roles.
//
// Usage example on a controller action:
//   [AuthorizeRoles(Role.RECRUITER)]
//   public async Task<IActionResult> CreateJob(...) { ... }
//
// If the user is a JOB_SEEKER calling CreateJob, they get HTTP 403.
// If the user is not logged in at all, they get HTTP 401.
// ============================================================

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using JobFinder.Api.Common.Models;

namespace JobFinder.Api.Middleware
{
    // 📌 CONCEPT: Attribute classes
    //    Adding ": Attribute" makes this class usable as [AuthorizeRoles(...)].
    //    "IAsyncAuthorizationFilter" tells ASP.NET Core to run this BEFORE
    //    the action method is invoked.
    //
    //    AttributeUsage restricts where the attribute can be applied:
    //      AttributeTargets.Class  → on the whole controller class
    //      AttributeTargets.Method → on individual action methods
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeRolesAttribute : Attribute, IAsyncAuthorizationFilter
    {
        // 📌 CONCEPT: params keyword
        //    "params Role[] roles" means the caller can pass any number of
        //    roles as comma-separated arguments:
        //      [AuthorizeRoles(Role.RECRUITER)]
        //      [AuthorizeRoles(Role.RECRUITER, Role.ADMIN)]
        private readonly string[] _roles;

        public AuthorizeRolesAttribute(params Role[] roles)
        {
            // Convert enum values to their string names for comparison with JWT claims.
            // Role.RECRUITER.ToString() → "RECRUITER"
            _roles = roles.Select(r => r.ToString()).ToArray();
        }

        // 📌 CONCEPT: IAsyncAuthorizationFilter.OnAuthorizationAsync
        //    ASP.NET Core calls this method before every action that has
        //    this attribute.  Setting context.Result short-circuits the
        //    pipeline — the action is never called if Result is set.
        //
        //    EXAMPLE flow for [AuthorizeRoles(Role.RECRUITER)]:
        //      Anonymous user  → AuthMiddleware sets no context.User
        //                     → userId is null → HTTP 401
        //      JOB_SEEKER user → userId is set, but role "RECRUITER" missing → HTTP 403
        //      RECRUITER user  → both checks pass → action runs → HTTP 200
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // FindFirst searches the token claims for the "userId" key.
            var userId = user.FindFirst("userId")?.Value;

            // ── Step 1: Is the user authenticated? ──────────────────
            if (string.IsNullOrEmpty(userId))
            {
                // Read the error reason stored by AuthMiddleware.
                // This gives a more specific message: "Missing access token"
                // vs "Invalid or expired access token".
                var authError = context.HttpContext.Items["AuthError"] as string ?? "Missing access token";

                // 📌 CONCEPT: Setting context.Result to short-circuit the pipeline
                //    JsonResult writes a JSON body.  StatusCode 401 means "Unauthenticated".
                context.Result = new JsonResult(new { error = authError })
                {
                    StatusCode = 401
                };
                return Task.CompletedTask;
            }

            // ── Step 2: Does the user have an allowed role? ──────────
            // 📌 CONCEPT: Reading multiple claims with the same key
            //    FindAll returns ALL claims matching the key.
            //    We check "roles", "role", and the standard ClaimTypes.Role
            //    for maximum compatibility.
            //
            //    EXAMPLE claims in JWT payload:
            //      { "userId": "u1", "roles": "JOB_SEEKER", "roles": "RECRUITER" }
            //    FindAll("roles") returns both → userRoles = {"JOB_SEEKER", "RECRUITER"}
            var userRoles = user.FindAll("roles")
                                .Concat(user.FindAll("role"))
                                .Concat(user.FindAll(ClaimTypes.Role))
                                .Select(c => c.Value)
                                .ToHashSet();  // 📌 HashSet for O(1) lookup

            // Does the user have ANY of the required roles?
            var hasRole = _roles.Any(role => userRoles.Contains(role));

            if (!hasRole)
            {
                // 📌 StatusCode 403 = "Authenticated but not authorised".
                context.Result = new JsonResult(new { error = "Access denied." })
                {
                    StatusCode = 403
                };
            }

            // Task.CompletedTask = synchronous completion (no actual async work needed).
            return Task.CompletedTask;
        }
    }
}
