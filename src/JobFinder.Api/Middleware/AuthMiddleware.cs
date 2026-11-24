// ============================================================
// FILE: Middleware/AuthMiddleware.cs
// CONCEPT: ASP.NET Core Middleware — the request pipeline
// ------------------------------------------------------------
// Middleware is code that runs for EVERY request, in order.
// Think of it like a series of filters the request passes
// through before reaching a controller.
//
//   Request → ErrorHandler → CORS → Auth → Controller
//                                           ↓
//   Response ← ErrorHandler ← CORS ← Auth ← Controller
//
// Each middleware calls "_next(context)" to pass control to
// the next one, or it can short-circuit and return early.
//
// This middleware reads the JWT from the HTTP-only cookie
// and populates context.User so controllers can call
// User.FindFirst("userId").
// ============================================================

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using JobFinder.Api.Utils;

namespace JobFinder.Api.Middleware
{
    public class AuthMiddleware
    {
        // 📌 CONCEPT: RequestDelegate
        //    "RequestDelegate" is a function signature: Task(HttpContext).
        //    _next represents the NEXT middleware in the pipeline.
        //    By calling "await _next(context)" we pass the request along.
        private readonly RequestDelegate _next;

        // 📌 CONCEPT: Middleware constructor receives only SINGLETON services
        //    _next is injected here (it never changes per request).
        //    Scoped services like DbContext are injected via the Invoke method instead.
        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // 📌 CONCEPT: Invoke(HttpContext, ...) — method injection for scoped services
        //    ASP.NET Core calls this method for every HTTP request.
        //    "IJwtHelper jwtHelper" is injected per-call by the DI container.
        //    This pattern allows using scoped services inside singleton middleware.
        //
        //    EXAMPLE request flow:
        //      Browser sends GET /api/auth/me
        //        with Cookie: accessToken=eyJ...
        //      → AuthMiddleware reads the cookie, verifies JWT
        //      → Sets context.User to the ClaimsPrincipal with userId + roles
        //      → Calls _next(context) → AuthController.GetMe() runs
        //      → Controller reads User.FindFirst("userId") successfully
        public async Task Invoke(HttpContext context, IJwtHelper jwtHelper)
        {
            // 📌 CONCEPT: Reading cookies
            //    Request.Cookies is a dictionary of cookie name → value.
            //    TryGetValue returns false if the cookie doesn't exist.
            if (context.Request.Cookies.TryGetValue("accessToken", out var token))
            {
                // Validate the JWT.  Returns null if expired or tampered with.
                var principal = jwtHelper.VerifyAccessToken(token);

                if (principal != null)
                {
                    // 📌 CONCEPT: context.User
                    //    Setting context.User makes the authenticated user
                    //    available throughout the rest of the pipeline via
                    //    "User.FindFirst(...)" in controllers.
                    context.User = principal;
                }
                else
                {
                    // 📌 CONCEPT: context.Items
                    //    A per-request dictionary for passing data between
                    //    middleware layers.  Here we store the error message
                    //    so [RequireAuth] and [AuthorizeRoles] can return a
                    //    meaningful 401 message instead of a generic one.
                    //
                    //    EXAMPLE: token was valid yesterday but expired today
                    //      → context.Items["AuthError"] = "Invalid or expired access token"
                    context.Items["AuthError"] = "Invalid or expired access token";
                }
            }
            else
            {
                // No cookie at all — anonymous request (e.g. public job listing).
                // Public endpoints don't use [RequireAuth] so they won't check this.
                context.Items["AuthError"] = "Missing access token";
            }

            // Always pass control to the next middleware regardless of auth status.
            // The [RequireAuth] / [AuthorizeRoles] attributes on individual actions
            // will enforce authentication where needed.
            await _next(context);
        }
    }
}
