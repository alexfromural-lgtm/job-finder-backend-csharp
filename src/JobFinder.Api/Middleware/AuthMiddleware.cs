using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using JobFinder.Api.Utils;

namespace JobFinder.Api.Middleware
{
    public class AuthMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IJwtHelper jwtHelper)
        {
            if (context.Request.Cookies.TryGetValue("accessToken", out var token))
            {
                var principal = jwtHelper.VerifyAccessToken(token);
                if (principal != null)
                {
                    context.User = principal;
                }
                else
                {
                    context.Items["AuthError"] = "Invalid or expired access token";
                }
            }
            else
            {
                context.Items["AuthError"] = "Missing access token";
            }

            await _next(context);
        }
    }
}
