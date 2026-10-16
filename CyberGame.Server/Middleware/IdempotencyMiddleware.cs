using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CyberGame.Domain.Entities;
using CyberGame.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Server.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CyberGameDbContext db)
    {
        // Only apply to POST requests
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.TrimEnd('/')?.ToLowerInvariant() ?? string.Empty;

        // Bypass auth login/refresh, health check, openapi
        if (path.StartsWith("/api/v1/auth") || path.StartsWith("/api/v1/member/auth") || path.StartsWith("/health") || path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        var isTransactional = IsTransactionalPost(path);
        if (!isTransactional)
        {
            await _next(context);
            return;
        }

        // Only apply to transactional endpoints that are authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var hasIdempotencyHeader = context.Request.Headers.TryGetValue("Idempotency-Key", out var headerVal)
                                   && !string.IsNullOrWhiteSpace(headerVal.ToString());

        if (!hasIdempotencyHeader)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Thiếu Idempotency-Key",
                Detail = "Header Idempotency-Key là bắt buộc đối với giao dịch này.",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = "idempotency_key_required";
            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        var idempotencyKey = headerVal.ToString().Trim();
        if (idempotencyKey.Length > 100 || !Guid.TryParse(idempotencyKey, out _))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Idempotency-Key không hợp lệ",
                Detail = "Header Idempotency-Key phải là chuỗi UUID hợp lệ và không vượt quá 100 ký tự.",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = "invalid_idempotency_key";
            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        var scope = GetScope(context, path);

        // Read and hash request body
        context.Request.EnableBuffering();
        string bodyText;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var rawToHash = $"{scope}|{bodyText}";
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToHash)));

        var existingRecord = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == idempotencyKey);

        if (existingRecord != null)
        {
            if (existingRecord.ExpiresAtUtc <= DateTime.UtcNow)
            {
                db.IdempotencyRecords.Remove(existingRecord);
                await db.SaveChangesAsync();
                existingRecord = null;
            }
        }

        if (existingRecord != null)
        {
            if (existingRecord.ResponseStatusCode == 0)
            {
                _logger.LogWarning("Idempotency key {Key} is currently in progress for {Scope}", idempotencyKey, scope);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/problem+json";
                var inProgressProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Giao dịch đang được xử lý",
                    Detail = "Yêu cầu với Idempotency-Key này đang được xử lý bởi một tiến trình khác.",
                    Instance = context.Request.Path
                };
                inProgressProblem.Extensions["code"] = "idempotency_in_progress";
                await context.Response.WriteAsJsonAsync(inProgressProblem);
                return;
            }

            if (!string.Equals(existingRecord.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Idempotency mismatch for key {Key} on {Scope}", idempotencyKey, scope);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/problem+json";
                var conflictProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Xung đột Idempotency-Key",
                    Detail = "Idempotency-Key này đã được sử dụng cho một yêu cầu có nội dung khác.",
                    Instance = context.Request.Path
                };
                conflictProblem.Extensions["code"] = "idempotency_conflict";
                await context.Response.WriteAsJsonAsync(conflictProblem);
                return;
            }

            // Replay stored response
            context.Response.StatusCode = existingRecord.ResponseStatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["X-Idempotent-Replay"] = "true";
            if (!string.IsNullOrEmpty(existingRecord.ResponseBody))
            {
                await context.Response.WriteAsync(existingRecord.ResponseBody);
            }
            return;
        }

        // Claim key BEFORE downstream execution with placeholder ResponseStatusCode = 0
        var placeholder = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatusCode = 0,
            ResponseBody = null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.IdempotencyRecords.Add(placeholder);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Concurrency conflict claiming key {Key} for scope {Scope}", idempotencyKey, scope);
            db.Entry(placeholder).State = EntityState.Detached;

            var concurrentRecord = await db.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == idempotencyKey);

            if (concurrentRecord != null)
            {
                if (concurrentRecord.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "application/problem+json";
                    var expProblem = new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Khóa đã hết hạn",
                        Detail = "Idempotency-Key đã hết hạn.",
                        Instance = context.Request.Path
                    };
                    expProblem.Extensions["code"] = "idempotency_expired";
                    await context.Response.WriteAsJsonAsync(expProblem);
                    return;
                }

                if (concurrentRecord.ResponseStatusCode == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "application/problem+json";
                    var progressProblem = new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Giao dịch đang được xử lý",
                        Detail = "Yêu cầu với Idempotency-Key này đang được xử lý bởi một tiến trình khác.",
                        Instance = context.Request.Path
                    };
                    progressProblem.Extensions["code"] = "idempotency_in_progress";
                    await context.Response.WriteAsJsonAsync(progressProblem);
                    return;
                }

                if (!string.Equals(concurrentRecord.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "application/problem+json";
                    var conflictProblem = new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Xung đột Idempotency-Key",
                        Detail = "Idempotency-Key này đã được sử dụng cho một yêu cầu có nội dung khác.",
                        Instance = context.Request.Path
                    };
                    conflictProblem.Extensions["code"] = "idempotency_conflict";
                    await context.Response.WriteAsJsonAsync(conflictProblem);
                    return;
                }

                // Replay
                context.Response.StatusCode = concurrentRecord.ResponseStatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.Headers["X-Idempotent-Replay"] = "true";
                if (!string.IsNullOrEmpty(concurrentRecord.ResponseBody))
                {
                    await context.Response.WriteAsync(concurrentRecord.ResponseBody);
                }
                return;
            }

            // Absolutely do not run downstream second time
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            var fallbackProblem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Giao dịch đang được xử lý",
                Detail = "Xung đột khóa idempotency trong quá trình xử lý.",
                Instance = context.Request.Path
            };
            fallbackProblem.Extensions["code"] = "idempotency_in_progress";
            await context.Response.WriteAsJsonAsync(fallbackProblem);
            return;
        }

        // Execute downstream and capture response
        var originalBodyStream = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        try
        {
            await _next(context);

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                memStream.Position = 0;
                var responseText = await new StreamReader(memStream, Encoding.UTF8).ReadToEndAsync();

                placeholder.ResponseStatusCode = context.Response.StatusCode;
                placeholder.ResponseBody = responseText;
                await db.SaveChangesAsync();

                // Copy response to client ONLY AFTER SaveChanges succeeded
                memStream.Position = 0;
                await memStream.CopyToAsync(originalBodyStream);
            }
            else
            {
                // Non-2xx response from downstream: delete placeholder
                try
                {
                    await RemovePlaceholderWithoutPersistingDownstreamChangesAsync(db, placeholder.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove placeholder for non-2xx response for key {Key}", idempotencyKey);
                }

                memStream.Position = 0;
                await memStream.CopyToAsync(originalBodyStream);
            }
        }
        catch (Exception)
        {
            // Exception in downstream: delete placeholder
            try
            {
                await RemovePlaceholderWithoutPersistingDownstreamChangesAsync(db, placeholder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove placeholder after downstream exception for key {Key}", idempotencyKey);
            }
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task RemovePlaceholderWithoutPersistingDownstreamChangesAsync(
        CyberGameDbContext db,
        Guid placeholderId)
    {
        // The middleware and controller share the request-scoped DbContext.
        // If downstream throws, it may have pending tracked mutations. Clear
        // them before saving placeholder cleanup, otherwise a failed request
        // could accidentally persist business data while removing its key.
        db.ChangeTracker.Clear();
        var persistedPlaceholder = await db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Id == placeholderId);
        if (persistedPlaceholder == null)
        {
            return;
        }

        db.IdempotencyRecords.Remove(persistedPlaceholder);
        await db.SaveChangesAsync();
    }

    private static bool IsTransactionalPost(string path)
    {
        if (path.Contains("/wallet/top-up") || path.Contains("/wallet/topup") || path.Contains("/wallet/adjust"))
            return true;
        if (path.EndsWith("/inventory/receive") || path.EndsWith("/inventory/adjust"))
            return true;
        if (path.EndsWith("/orders") || path.EndsWith("/confirm") || path.EndsWith("/cancel") || path.EndsWith("/fulfill"))
            return true;
        if (path.EndsWith("/sessions") || path.EndsWith("/sessions/start") || path.EndsWith("/pause") || path.EndsWith("/resume") || path.EndsWith("/transfer") || path.EndsWith("/complete"))
            return true;
        if (path.Contains("/invoices/") && (path.EndsWith("/payments") || path.EndsWith("/void")))
            return true;
        if (path.EndsWith("/shifts/open") || path.EndsWith("/close") || path.EndsWith("/cash-movements"))
            return true;
        if (path.EndsWith("/top-up-requests") || path.Contains("/member-top-up-requests") || path.EndsWith("/member-pin"))
            return true;
        if (path == "/api/v1/customers" || path == "/api/v1/products")
            return true;

        return false;
    }

    private static string GetScope(HttpContext context, string path)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User?.FindFirst("sub")?.Value
                  ?? "anonymous";
        var branchId = context.User?.FindFirst("branch_id")?.Value ?? "default";
        var method = context.Request.Method.ToUpperInvariant();
        return $"{userId}:{branchId}:{method}:{path}";
    }
}
