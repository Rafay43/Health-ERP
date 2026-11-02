using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using HealthERP.Application.Pharmacy;
using HealthERP.Application.Billing;
using HealthERP.Application.Inpatient;
using HealthERP.Application.PatientAccount;
using HealthERP.Application.StaffMembers;

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
            if (ex is not (UnauthorizedAccessException or StaffAccessDeniedException or PharmacyDispenseConflictException or BillingPaymentConflictException or InpatientConflictException))
                _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail) = exception switch
        {
            BadHttpRequestException => (HttpStatusCode.BadRequest, "Invalid Request", "Request body or parameters are invalid."),
            StaffAccountForbiddenException => (HttpStatusCode.Forbidden, "Staff Account Access Denied", "Admin permission is required to manage staff accounts."),
            StaffAccountConflictException conflict => (HttpStatusCode.Conflict, "Staff Account Conflict", conflict.Message),
            StaffAccountStorageException => (HttpStatusCode.InternalServerError, "Staff Account Change Failed", "Unable to confirm the account change. Refresh staff and account state before retrying."),
            StaffAccessDeniedException => (
                HttpStatusCode.Forbidden, "Staff Access Denied", "An eligible staff association is required for this operation."),
            ActivationConflictException => (
                HttpStatusCode.Conflict, "Activation Conflict", "Activation state changed. Refresh account status before retrying."),
            ActivationStorageException => (
                HttpStatusCode.InternalServerError, "Activation Failed", "Unable to confirm activation. Refresh account status before retrying."),
            InpatientConflictException conflict => (
                HttpStatusCode.Conflict, "Inpatient Conflict", conflict.Message),
            InpatientStorageException => (
                HttpStatusCode.InternalServerError, "Inpatient Operation Failed",
                "Unable to confirm the inpatient operation. Refresh beds and admissions before retrying."),
            BillingPaymentConflictException conflict => (
                HttpStatusCode.Conflict, "Payment Conflict", conflict.Message),
            BillingPaymentStorageException => (
                HttpStatusCode.InternalServerError, "Payment Failed",
                "Unable to confirm payment. Refresh the invoice and payment history before retrying."),
            PharmacyDispenseConflictException conflict => (
                HttpStatusCode.Conflict, "Dispensing Conflict", conflict.Message),
            PharmacyDispenseStorageException => (
                HttpStatusCode.InternalServerError, "Dispensing Failed",
                "Unable to confirm dispensing. Refresh the prescription and inventory before retrying."),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Authentication Failed",
                "Invalid credentials."),

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

            InvalidOperationException invEx => (
                HttpStatusCode.Conflict,
                "Scheduling Conflict",
                invEx.Message),

            KeyNotFoundException keyEx => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                keyEx.Message),

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
