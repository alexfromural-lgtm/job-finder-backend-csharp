using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using JobFinder.Api.Common.Exceptions;

namespace JobFinder.Api.Middleware
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception error)
            {
                var response = context.Response;
                response.ContentType = "application/json";

                var statusCode = (int)HttpStatusCode.InternalServerError;
                var message = "Internal server error";

                switch (error)
                {
                    case AppException appEx:
                        // Custom application-level exception
                        statusCode = appEx.StatusCode;
                        message = appEx.Message;
                        break;

                    default:
                        // Check if it's a PostgreSQL unique constraint violation (code 23505)
                        if (error.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                        {
                            statusCode = (int)HttpStatusCode.Conflict;
                            message = "A record with that value already exists.";
                        }
                        else
                        {
                            // Log unexpected errors
                            _logger.LogError(error, "[Unhandled Error] An unexpected error occurred.");
                        }
                        break;
                }

                response.StatusCode = statusCode;
                
                var result = JsonSerializer.Serialize(new { error = message });
                await response.WriteAsync(result);
            }
        }
    }
}
