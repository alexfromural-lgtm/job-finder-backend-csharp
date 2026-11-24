// ============================================================
// FILE: Common/Exceptions/AppException.cs
// CONCEPT: Custom Exception Class
// ------------------------------------------------------------
// In C# you can create your own exception types by inheriting
// from the built-in Exception class.  Doing so lets you attach
// extra data (here: an HTTP status code) to an error and
// catch it specifically in middleware without catching
// every possible exception.
// ============================================================

using System;

namespace JobFinder.Api.Common.Exceptions
{
    // 📌 CONCEPT: Inheritance
    //    "AppException : Exception" means AppException IS-AN Exception.
    //    It gains all built-in exception behaviour (Message, StackTrace …)
    //    and we add our own StatusCode property on top.
    //
    //    EXAMPLE:
    //      throw new AppException("User not found", 404);
    //      // → ErrorHandlerMiddleware catches it, reads StatusCode = 404
    //      //   and returns HTTP 404 {"error":"User not found"} to the client.
    public class AppException : Exception
    {
        // 📌 CONCEPT: Auto-property with getter only
        //    "public int StatusCode { get; }" declares a read-only property.
        //    It can only be set inside this class (via the constructor below).
        //
        //    EXAMPLE:
        //      var ex = new AppException("Not found", 404);
        //      Console.WriteLine(ex.StatusCode); // → 404
        public int StatusCode { get; }

        // 📌 CONCEPT: Constructor chaining with ": base(message)"
        //    "base(message)" calls the Exception constructor which stores
        //    'message' so that ex.Message returns it.
        //    The default value "= 400" means callers can omit the status code
        //    and it defaults to 400 Bad Request.
        //
        //    EXAMPLES:
        //      throw new AppException("Email is required");        // StatusCode = 400
        //      throw new AppException("User not found", 404);      // StatusCode = 404
        //      throw new AppException("Account deactivated", 403); // StatusCode = 403
        public AppException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
