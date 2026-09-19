using InterLan.Contracts;
using InterLan.Infrastructure;
using InterLan.Server.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server;

public static class GroupEndpointMappings
{
    public static IEndpointRouteBuilder MapInterLanGroupEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/groups", async (
            CreateGroupRequest request,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.CreateGroupAsync(
                    principal.UserId,
                    request,
                    cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapGet("/api/v1/groups", async (
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            return Results.Ok(await groups.ListGroupsAsync(
                principal.UserId,
                cancellationToken));
        });

        app.MapGet("/api/v1/groups/{groupId:guid}", async (
            Guid groupId,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.GetGroupDetailsAsync(
                    principal.UserId,
                    groupId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        app.MapPut("/api/v1/groups/{groupId:guid}", async (
            Guid groupId,
            UpdateGroupRequest request,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.UpdateGroupAsync(
                    principal.UserId,
                    groupId,
                    request,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapGet("/api/v1/groups/{groupId:guid}/events", async (
            Guid groupId,
            Guid? afterEventId,
            int? limit,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.ListGroupEventsAsync(
                    principal.UserId,
                    groupId,
                    afterEventId,
                    Math.Clamp(limit ?? 100, 1, 250),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        app.MapPost("/api/v1/groups/{groupId:guid}/members", async (
            Guid groupId,
            AddGroupMemberRequest request,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.AddMemberAsync(
                    principal.UserId,
                    groupId,
                    request,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapDelete("/api/v1/groups/{groupId:guid}/members/{userId:guid}", async (
            Guid groupId,
            Guid userId,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.RemoveMemberAsync(
                    principal.UserId,
                    groupId,
                    userId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        });

        app.MapPut("/api/v1/groups/{groupId:guid}/members/{userId:guid}/role", async (
            Guid groupId,
            Guid userId,
            UpdateGroupMemberRoleRequest request,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.UpdateMemberRoleAsync(
                    principal.UserId,
                    groupId,
                    userId,
                    request,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapGet("/api/v1/groups/{groupId:guid}/messages", async (
            Guid groupId,
            Guid? afterMessageId,
            int? limit,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                return Results.Ok(await groups.GetGroupHistoryPageAsync(
                    principal.UserId,
                    groupId,
                    afterMessageId,
                    Math.Clamp(limit ?? 100, 1, GroupStore.MaxMessagePageSize),
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        app.MapPost("/api/v1/groups/{groupId:guid}/messages", async (
            Guid groupId,
            SendMessageRequest request,
            HttpContext context,
            EnrollmentStore enrollment,
            GroupStore groups,
            IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            var principal = await AuthorizationHelpers.GetPrincipalAsync(
                context,
                enrollment,
                cancellationToken);
            if (principal is null)
                return Results.Unauthorized();

            try
            {
                var persisted = await groups.SendGroupMessageAsync(
                    principal.UserId,
                    groupId,
                    request,
                    cancellationToken);

                if (persisted.Created)
                {
                    var memberIds = await groups.GetActiveGroupMemberIdsAsync(
                        principal.UserId,
                        groupId,
                        cancellationToken);

                    foreach (var memberId in memberIds)
                    {
                        await hub.Clients.Group(ChatHub.UserGroup(memberId))
                            .SendAsync(
                                "MessageCreated",
                                persisted.Message,
                                cancellationToken);
                    }
                }

                return Results.Ok(persisted.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("message");

        return app;
    }
}
