using System.IO;
using System.Security.Claims;
using System.Text;
using CyberGame.Domain.Entities;
using CyberGame.Infrastructure.Data;
using CyberGame.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CyberGame.Tests;

public class IdempotencyTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    private (CyberGameDbContext Db, SqliteConnection Connection) CreateSqliteDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new CyberGameDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(Guid userId, Guid branchId, string username = "testuser", string role = "Cashier")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("branch_id", branchId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task RequiresKey_ForAuthenticatedTransactionalEndpoints()
    {
        using var db = CreateInMemoryDb();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(Guid.NewGuid(), Guid.NewGuid());
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/sessions";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("12345")]
    public async Task RejectsInvalidKey_IfNotValidUuidOrTooLong(string invalidKey)
    {
        using var db = CreateInMemoryDb();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(Guid.NewGuid(), Guid.NewGuid());
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/sessions";
        context.Request.Headers["Idempotency-Key"] = invalidKey;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Bypasses_ForUnauthenticatedRequests()
    {
        using var db = CreateInMemoryDb();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        // context.User is unauthenticated
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/sessions";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        // Middleware bypasses unauthenticated requests so downstream authorization handles 401
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Bypasses_ForNonTransactionalEndpoints()
    {
        using var db = CreateInMemoryDb();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(Guid.NewGuid(), Guid.NewGuid());
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/categories"; // Not transactional
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ReplaysCachedResponse_WhenSameKeyAndSameBody()
    {
        using var db = CreateInMemoryDb();
        int downstreamCallCount = 0;
        RequestDelegate next = async (ctx) =>
        {
            downstreamCallCount++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes("{\"result\":\"success\"}");
            await ctx.Response.Body.WriteAsync(bytes);
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        var key = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // 1. Call 1: downstream executes, 200 response cached
        var context1 = new DefaultHttpContext();
        context1.User = CreateAuthenticatedUser(userId, branchId);
        context1.Request.Method = "POST";
        context1.Request.Path = "/api/v1/sessions";
        context1.Request.Headers["Idempotency-Key"] = key;
        var body1 = Encoding.UTF8.GetBytes("{\"machineId\":\"m1\"}");
        context1.Request.Body = new MemoryStream(body1);
        context1.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context1, db);

        Assert.Equal(1, downstreamCallCount);
        Assert.Equal(StatusCodes.Status200OK, context1.Response.StatusCode);

        // 2. Call 2 (Replay): same key, same body, same user -> cached response replayed, downstream not called
        var context2 = new DefaultHttpContext();
        context2.User = CreateAuthenticatedUser(userId, branchId);
        context2.Request.Method = "POST";
        context2.Request.Path = "/api/v1/sessions";
        context2.Request.Headers["Idempotency-Key"] = key;
        var body2 = Encoding.UTF8.GetBytes("{\"machineId\":\"m1\"}");
        context2.Request.Body = new MemoryStream(body2);
        context2.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context2, db);

        Assert.Equal(1, downstreamCallCount); // Did not increase!
        Assert.Equal(StatusCodes.Status200OK, context2.Response.StatusCode);
        Assert.Equal("true", context2.Response.Headers["X-Idempotent-Replay"].ToString());

        context2.Response.Body.Position = 0;
        using var reader = new StreamReader(context2.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.Contains("success", responseBody);
    }

    [Fact]
    public async Task Returns409Conflict_WhenSameKeyDifferentBody()
    {
        using var db = CreateInMemoryDb();
        int downstreamCallCount = 0;
        RequestDelegate next = async (ctx) =>
        {
            downstreamCallCount++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes("{\"result\":\"success\"}");
            await ctx.Response.Body.WriteAsync(bytes);
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        var key = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // 1. First call succeeds
        var context1 = new DefaultHttpContext();
        context1.User = CreateAuthenticatedUser(userId, branchId);
        context1.Request.Method = "POST";
        context1.Request.Path = "/api/v1/sessions";
        context1.Request.Headers["Idempotency-Key"] = key;
        var body1 = Encoding.UTF8.GetBytes("{\"machineId\":\"m1\"}");
        context1.Request.Body = new MemoryStream(body1);
        context1.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context1, db);
        Assert.Equal(1, downstreamCallCount);

        // 2. Second call with different body -> 409 Conflict
        var context2 = new DefaultHttpContext();
        context2.User = CreateAuthenticatedUser(userId, branchId);
        context2.Request.Method = "POST";
        context2.Request.Path = "/api/v1/sessions";
        context2.Request.Headers["Idempotency-Key"] = key;
        var body2 = Encoding.UTF8.GetBytes("{\"machineId\":\"m2_different\"}");
        context2.Request.Body = new MemoryStream(body2);
        context2.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context2, db);

        Assert.Equal(1, downstreamCallCount); // Downstream not called again!
        Assert.Equal(StatusCodes.Status409Conflict, context2.Response.StatusCode);
    }

    [Fact]
    public async Task DoesNotReplay_WhenDifferentUserOrBranch()
    {
        using var db = CreateInMemoryDb();
        int downstreamCallCount = 0;
        RequestDelegate next = async (ctx) =>
        {
            downstreamCallCount++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes($"{{\"callCount\":{downstreamCallCount}}}");
            await ctx.Response.Body.WriteAsync(bytes);
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        var key = Guid.NewGuid().ToString("D");
        var body = Encoding.UTF8.GetBytes("{\"amount\":10000}");

        // Call from User 1 in Branch 1
        var user1 = Guid.NewGuid();
        var branch1 = Guid.NewGuid();
        var context1 = new DefaultHttpContext();
        context1.User = CreateAuthenticatedUser(user1, branch1);
        context1.Request.Method = "POST";
        context1.Request.Path = "/api/v1/sessions";
        context1.Request.Headers["Idempotency-Key"] = key;
        context1.Request.Body = new MemoryStream(body);
        context1.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context1, db);
        Assert.Equal(1, downstreamCallCount);

        // Call from User 2 with SAME key and SAME body
        var user2 = Guid.NewGuid();
        var context2 = new DefaultHttpContext();
        context2.User = CreateAuthenticatedUser(user2, branch1);
        context2.Request.Method = "POST";
        context2.Request.Path = "/api/v1/sessions";
        context2.Request.Headers["Idempotency-Key"] = key;
        context2.Request.Body = new MemoryStream(body);
        context2.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context2, db);

        // Downstream MUST be called because scope includes UserId, preventing replay across users
        Assert.Equal(2, downstreamCallCount);
        Assert.Equal(StatusCodes.Status200OK, context2.Response.StatusCode);
        Assert.False(context2.Response.Headers.ContainsKey("X-Idempotent-Replay"));
    }

    [Fact]
    public async Task Returns409InProgress_WhenPlaceholderPending()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var path = "/api/v1/sessions";
        var scope = $"{userId}:{branchId}:POST:{path}";

        // Seed a pending placeholder (ResponseStatusCode = 0)
        var placeholder = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = "dummyhash",
            ResponseStatusCode = 0,
            ResponseBody = null,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.IdempotencyRecords.Add(placeholder);
        await db.SaveChangesAsync();

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(userId, branchId);
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.False(nextCalled); // Downstream MUST NOT be called!
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task DoesNotReplay_WhenRecordExpired()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var path = "/api/v1/sessions";
        var scope = $"{userId}:{branchId}:POST:{path}";

        // Seed an expired record
        var expiredRecord = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = "oldhash",
            ResponseStatusCode = StatusCodes.Status200OK,
            ResponseBody = "{\"old\":\"data\"}",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // Expired!
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8)
        };
        db.IdempotencyRecords.Add(expiredRecord);
        await db.SaveChangesAsync();

        int downstreamCallCount = 0;
        RequestDelegate next = async (ctx) =>
        {
            downstreamCallCount++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{\"fresh\":\"data\"}"));
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(userId, branchId);
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        // Should NOT replay expired response, should call downstream freshly
        Assert.Equal(1, downstreamCallCount);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var text = await reader.ReadToEndAsync();
        Assert.Contains("fresh", text);
    }

    [Fact]
    public async Task DoesNotCache_WhenDownstreamFailsNon2xx()
    {
        using var db = CreateInMemoryDb();
        var key = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(userId, branchId);
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/sessions";
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // Placeholder must be deleted, no record remaining in db
        var records = await db.IdempotencyRecords.ToListAsync();
        Assert.Empty(records);
    }

    [Fact]
    public async Task DownstreamException_DoesNotPersistTrackedBusinessChangesDuringPlaceholderCleanup()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();

        RequestDelegate next = _ =>
        {
            db.Branches.Add(new Branch
            {
                Id = branchId,
                Code = "SHOULD-NOT-PERSIST",
                Name = "Pending mutation from failed request"
            });
            throw new InvalidOperationException("Simulated downstream failure");
        };

        var middleware = new IdempotencyMiddleware(next, NullLogger<IdempotencyMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.User = CreateAuthenticatedUser(Guid.NewGuid(), Guid.NewGuid());
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/sessions";
        context.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString("D");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Response.Body = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, db));

        Assert.False(await db.Branches.AnyAsync(b => b.Id == branchId));
        Assert.Empty(await db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentRequests_EnforcingUniqueConstraint_WinsAndBlocksSecond()
    {
        var (db, connection) = CreateSqliteDb();
        using var conn = connection;
        using var dbContext = db;

        var key = Guid.NewGuid().ToString("D");
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var path = "/api/v1/sessions";

        int downstreamCalls = 0;
        var downstreamTcs = new TaskCompletionSource<bool>();

        RequestDelegate slowDownstream = async (ctx) =>
        {
            Interlocked.Increment(ref downstreamCalls);
            // Simulate long-running request
            await downstreamTcs.Task;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"));
        };

        var middleware = new IdempotencyMiddleware(slowDownstream, NullLogger<IdempotencyMiddleware>.Instance);

        // Thread 1: starts request, claims placeholder, holds in downstream
        var context1 = new DefaultHttpContext();
        context1.User = CreateAuthenticatedUser(userId, branchId);
        context1.Request.Method = "POST";
        context1.Request.Path = path;
        context1.Request.Headers["Idempotency-Key"] = key;
        context1.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"param\":1}"));
        context1.Response.Body = new MemoryStream();

        var task1 = Task.Run(async () =>
        {
            // Use separate db context sharing the same SQLite connection
            var options = new DbContextOptionsBuilder<CyberGameDbContext>()
                .UseSqlite(conn)
                .Options;
            using var threadDb = new CyberGameDbContext(options);
            await middleware.InvokeAsync(context1, threadDb);
        });

        // Wait a short moment to ensure task1 claimed the placeholder
        await Task.Delay(200);

        // Thread 2: attempts to run with SAME key and SAME user
        var context2 = new DefaultHttpContext();
        context2.User = CreateAuthenticatedUser(userId, branchId);
        context2.Request.Method = "POST";
        context2.Request.Path = path;
        context2.Request.Headers["Idempotency-Key"] = key;
        context2.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"param\":1}"));
        context2.Response.Body = new MemoryStream();

        var options2 = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseSqlite(conn)
            .Options;
        using (var threadDb2 = new CyberGameDbContext(options2))
        {
            await middleware.InvokeAsync(context2, threadDb2);
        }

        // Thread 2 must receive 409 Conflict (idempotency_in_progress)
        Assert.Equal(StatusCodes.Status409Conflict, context2.Response.StatusCode);

        // Now let thread 1 finish
        downstreamTcs.SetResult(true);
        await task1;

        Assert.Equal(StatusCodes.Status200OK, context1.Response.StatusCode);
        // Crucially: downstream was called exactly ONCE!
        Assert.Equal(1, downstreamCalls);
    }
}
