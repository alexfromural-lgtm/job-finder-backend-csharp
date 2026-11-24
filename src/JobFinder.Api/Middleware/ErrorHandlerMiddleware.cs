// ============================================================
// FILE: Middleware/ErrorHandlerMiddleware.cs
// CONCEPT: Global error handling middleware
// ------------------------------------------------------------
// Without this middleware, an unhandled exception would return
// an empty 500 response (or the default ASP.NET developer
// page, which leaks stack traces in production).
//
// By wrapping the entire pipeline in a try/catch, we can:
//   1. Convert known AppException to the appropriate HTTP status.
//   2. Convert PostgreSQL unique-constraint errors to HTTP 409.
//   3. Log unexpected errors.
//   4. Always return a JSON { "error": "..." } body the client
//      can parse predictably.
// ============================================================

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

        // 📌 CONCEPT: ILogger<T>
        //    ASP.NET Core's built-in logging abstraction.
        //    "ILogger<ErrorHandlerMiddleware>" scopes log entries
        //    to this class so you can filter logs by category.
        //
        //    EXAMPLE log output (console):
        //      info: ErrorHandlerMiddleware[0]
        //            [Unhandled Error] An unexpected error occurred.
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        // 📌 CONCEPT: try/catch wrapping the entire request pipeline
        //    "await _next(context)" runs ALL subsequent middleware AND
        //    the controller action.  If anything throws, we catch it here.
        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);  // proceed through the pipeline normally
            }
            catch (Exception error)
            {
                var response = context.Response;
                response.ContentType = "application/json";

                // Start with a generic 500 — override below for known cases.
                var statusCode = (int)HttpStatusCode.InternalServerError;
                var message = "Internal server error";

                // 📌 CONCEPT: Pattern matching with switch expression
                //    "switch (error)" with "case AppException appEx:" checks
                //    whether "error" IS-AN AppException and, if so, casts it.
                switch (error)
                {
                    case AppException appEx:
                        // 📌 Our custom exception — use its pre-set status code and message.
                        //
                        //    EXAMPLE:
                        //      throw new AppException("User not found", 404);
                        //      → HTTP 404  {"error":"User not found"}
                        statusCode = appEx.StatusCode;
                        message = appEx.Message;
                        break;

                    default:
                        // 📌 CONCEPT: InnerException chaining
                        //    EF Core often wraps a PostgresException inside
                        //    a DbUpdateException.  We check the InnerException
                        //    to detect the underlying Postgres error.
                        //
                        //    SqlState "23505" = unique_violation in PostgreSQL.
                        //    EXAMPLE: inserting a user with a duplicate email
                        //      → HTTP 409  {"error":"A record with that value already exists."}
                        if (error.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
                        {
                            statusCode = (int)HttpStatusCode.Conflict;
                            message = "A record with that value already exists.";
                        }
                        else
                        {
                            // Truly unexpected — log it so it shows up in monitoring.
                            // The client only sees "Internal server error" (no stack trace).
                            _logger.LogError(error, "[Unhandled Error] An unexpected error occurred.");
                        }
                        break;
                }

                // Write the HTTP status code.
                response.StatusCode = statusCode;

                // 📌 CONCEPT: JsonSerializer.Serialize
                //    Converts the anonymous object to a JSON string.
                //    EXAMPLE output: {"error":"User not found"}
                var result = JsonSerializer.Serialize(new { error = message });
                await response.WriteAsync(result);
            }
        }
    }
}
