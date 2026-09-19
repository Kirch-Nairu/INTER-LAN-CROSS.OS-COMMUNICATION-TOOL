using System.Net;
using InterLan.Application;
using InterLan.Contracts;
using InterLan.Infrastructure;
using InterLan.Server.Realtime;
using Microsoft.Data.Sqlite;

namespace InterLan.Server;

public static class EnrollmentEndpointMappings
{
    public static IEndpointRouteBuilder MapInterLanEnrollmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/bootstrap/server", async (
            HttpContext context,
            BootstrapServerRequest request,
            EnrollmentStore enrollment,
            ServerRuntimeSettings settings,
            ServerCertificateDescriptor cert,
            CancellationToken cancellationToken) =>
        {
            var remote = context.Connection.RemoteIpAddress;
            if (remote is null || !IPAddress.IsLoopback(remote))
                return Results.Forbid();
        
            try
            {
                var identity = await enrollment.BootstrapOwnerAsync(
                    request.ServerName,
                    request.OwnerUsername,
                    request.OwnerDisplayName,
                    request.OwnerPassword,
                    settings.StoragePath,
                    settings.Port,
                    settings.DiscoveryEnabled,
                    cancellationToken);
        
                return Results.Ok(new BootstrapServerResponse(
                    identity.ServerId,
                    identity.OwnerUserId,
                    identity.ServerName,
                    cert.Sha256Fingerprint));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or SqliteException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("auth");
        
        app.MapPost("/api/v1/auth/owner/login", async (
            LoginRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await enrollment.LoginOwnerAsync(
                    request.Username,
                    request.Password,
                    TimeSpan.FromHours(12),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireRateLimiting("auth");
        
        app.MapGet("/api/v1/auth/device/current", async (
            HttpContext context,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                if (principal.DeviceId is not { } deviceId)
                    return Results.Conflict(new { error = "Current session is not device-bound." });

                return Results.Ok(await enrollment.GetCurrentDeviceSecurityAsync(
                    principal.UserId,
                    deviceId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        app.MapGet("/api/v1/auth/sessions", async (
            HttpContext context,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                return Results.Ok(await enrollment.ListOwnSessionsAsync(
                    principal.UserId,
                    principal.SessionId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/auth/sessions/revoke-others", async (
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                var revoked = await enrollment.RevokeOtherSessionsAsync(
                    principal.UserId,
                    principal.SessionId,
                    cancellationToken);

                foreach (var sessionId in revoked)
                    realtimeConnections.RevokeSession(sessionId);

                return Results.Ok(new RevokeOtherSessionsResponse(revoked.Count));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        app.MapPost("/api/v1/auth/sessions/{sessionId:guid}/revoke", async (
            Guid sessionId,
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                await enrollment.RevokeOwnSessionAsync(
                    principal.UserId,
                    sessionId,
                    cancellationToken);

                realtimeConnections.RevokeSession(sessionId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/auth/device/unpair", async (
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                if (principal.DeviceId is not { } deviceId)
                    return Results.Conflict(new { error = "Current session is not device-bound." });

                await enrollment.RevokeOwnDeviceAsync(
                    principal.UserId,
                    principal.SessionId,
                    deviceId,
                    cancellationToken);

                realtimeConnections.RevokeDevice(deviceId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        app.MapPost("/api/v1/auth/logout", async (
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                await enrollment.RevokeOwnSessionAsync(
                    principal.UserId,
                    principal.SessionId,
                    cancellationToken);

                realtimeConnections.RevokeSession(principal.SessionId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/auth/device/renew", async (
            RenewDeviceSessionRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await enrollment.RenewDeviceSessionAsync(
                    request.DeviceId,
                    request.DeviceCredential,
                    TimeSpan.FromHours(12),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireRateLimiting("auth");
        
        app.MapPost("/api/v1/invites", async (
            HttpContext context,
            CreateInviteRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                var minutes = Math.Clamp(request.ValidMinutes, 1, 10_080);
                return Results.Ok(await enrollment.CreateInviteAsync(
                    owner.UserId,
                    TimeSpan.FromMinutes(minutes),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/join", async (
            SubmitJoinRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Accepted(
                    value: await enrollment.SubmitJoinAsync(request, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("join");
        
        app.MapGet("/api/v1/join/pending", async (
            HttpContext context,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                return Results.Ok(await enrollment.ListPendingJoinsAsync(owner.UserId, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapGet("/api/v1/devices", async (
            HttpContext context,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                return Results.Ok(await enrollment.ListDevicesAsync(owner.UserId, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/join/{requestId:guid}/decision", async (
            Guid requestId,
            HttpContext context,
            JoinDecisionRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                return Results.Ok(await enrollment.DecideJoinAsync(
                    owner.UserId,
                    requestId,
                    request.Approve,
                    request.ExistingUserId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or SqliteException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        
        app.MapPost("/api/v1/join/exchange", async (
            ExchangeJoinRequest request,
            EnrollmentStore enrollment,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await enrollment.ExchangeApprovedJoinAsync(
                    request.RequestId,
                    request.EnrollmentSecret,
                    TimeSpan.FromDays(14),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireRateLimiting("join");
        
        app.MapPost("/api/v1/sessions/{sessionId:guid}/revoke", async (
            Guid sessionId,
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                await enrollment.RevokeSessionAsync(owner.UserId, sessionId, cancellationToken);
                realtimeConnections.RevokeSession(sessionId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        
        app.MapPost("/api/v1/devices/{deviceId:guid}/credential/rotate", async (
            Guid deviceId,
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                var session = await enrollment.RotateDeviceCredentialAsync(
                    principal.UserId,
                    principal.SessionId,
                    deviceId,
                    TimeSpan.FromHours(12),
                    cancellationToken);
        
                realtimeConnections.RevokeDevice(deviceId);
                return Results.Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireRateLimiting("auth");
        
        app.MapPost("/api/v1/devices/{deviceId:guid}/revoke", async (
            Guid deviceId,
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry realtimeConnections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var owner = await AuthorizationHelpers.RequireOwnerAsync(context, enrollment, cancellationToken);
                await enrollment.RevokeDeviceAsync(owner.UserId, deviceId, cancellationToken);
                realtimeConnections.RevokeDevice(deviceId);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        

        return app;
    }
}
