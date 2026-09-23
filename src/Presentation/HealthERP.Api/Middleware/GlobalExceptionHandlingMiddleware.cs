using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HealthERP.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation Failed",
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),

            DbUpdateException dbEx when dbEx.InnerException is PostgresException pgEx
                && pgEx.SqlState == "23505" => (
                HttpStatusCode.Conflict,
                "Duplicate Record",
                "A record with this unique value already exists."),

            DbUpdateException => (
                HttpStatusCode.BadRequest,
                "Database Update Failed",
                "The request could not be processed due to a data constraint violation."),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                "Invalid Argument",
                argEx.Message),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred",
                "Please try again later or contact support if the problem persists.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}
