using System.Diagnostics;
using System.Text.Json;
using CyberGame.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var statusCode = StatusCodes.Status500InternalServerError;
        string code = "internal_server_error";
        string title = "Đã xảy ra lỗi hệ thống";
        string detail = ex.Message;

        switch (ex)
        {
            case NotFoundException nfe:
                statusCode = StatusCodes.Status404NotFound;
                code = nfe.Code;
                title = "Không tìm thấy dữ liệu";
                break;

            case ConflictException ce:
                statusCode = StatusCodes.Status409Conflict;
                code = ce.Code;
                title = "Xung đột dữ liệu";
                break;

            case Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
                statusCode = StatusCodes.Status409Conflict;
                code = "concurrency_conflict";
                title = "Xung đột đồng thời (Concurrency Conflict)";
                detail = "Dữ liệu đã bị thay đổi bởi một tác vụ khác. Vui lòng làm mới dữ liệu và thử lại.";
                break;

            case BusinessRuleException bre:
                statusCode = StatusCodes.Status400BadRequest;
                code = bre.Code;
                title = "Vi phạm quy tắc nghiệp vụ";
                break;

            case DomainException de:
                statusCode = StatusCodes.Status400BadRequest;
                code = de.Code;
                title = "Lỗi xử lý nghiệp vụ";
                break;

            default:
                _logger.LogError(ex, "Lỗi chưa được xử lý tại {Path}: {Message}", context.Request.Path, ex.Message);
                detail = "Vui lòng liên hệ quản trị viên để được hỗ trợ.";
                break;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["code"] = code;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
