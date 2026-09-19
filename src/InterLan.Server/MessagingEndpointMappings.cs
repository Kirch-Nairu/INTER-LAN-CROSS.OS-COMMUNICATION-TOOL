using InterLan.Contracts;
using InterLan.Infrastructure;
using InterLan.Server.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server;

public static class MessagingEndpointMappings
{
    public static IEndpointRouteBuilder MapInterLanMessagingEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", async (
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                return Results.Ok(await chat.ListUsersAsync(principal.UserId, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapGet("/api/v1/direct", async (
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                return Results.Ok(await chat.ListDirectConversationsAsync(principal.UserId, cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapGet("/api/v1/direct/summaries", async (
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                return Results.Ok(
                    await chat.ListDirectConversationSummariesAsync(
                        principal.UserId,
                        cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPost("/api/v1/direct", async (
            HttpContext context,
            OpenDirectConversationRequest request,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                return Results.Ok(await chat.GetOrCreateDirectConversationAsync(
                    principal.UserId,
                    request.OtherUserId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        
        app.MapGet("/api/v1/direct/{conversationId:guid}/messages", async (
            Guid conversationId,
            Guid? afterMessageId,
            int? limit,
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                return Results.Ok(await chat.GetDirectHistoryAsync(
                    principal.UserId,
                    conversationId,
                    afterMessageId,
                    Math.Clamp(limit ?? 100, 1, 250),
                    cancellationToken));
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
        
        app.MapPost("/api/v1/direct/{conversationId:guid}/messages", async (
            Guid conversationId,
            HttpContext context,
            SendMessageRequest request,
            EnrollmentStore enrollment,
            ChatStore chat,
            Microsoft.AspNetCore.SignalR.IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                var result = await chat.SendDirectMessageAsync(
                    principal.UserId,
                    conversationId,
                    request,
                    cancellationToken);
        
                if (result.Created)
                {
                    var members = await chat.GetDirectMemberIdsAsync(principal.UserId, conversationId, cancellationToken);
                    foreach (var memberId in members)
                    {
                        await hub.Clients.Group(ChatHub.UserGroup(memberId))
                            .SendAsync("MessageCreated", result.Message, cancellationToken);
                    }
                }
        
                return Results.Ok(result.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        }).RequireRateLimiting("message");
        
        app.MapGet("/api/v1/messages/{messageId:guid}/receipts", async (
            Guid messageId,
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                return Results.Ok(await chat.GetReceiptsAsync(principal.UserId, messageId, cancellationToken));
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
        
        app.MapPost("/api/v1/messages/{messageId:guid}/delivered", async (
            Guid messageId,
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                await chat.MarkDeliveredAsync(principal.UserId, messageId, cancellationToken);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        
        app.MapPost("/api/v1/messages/{messageId:guid}/read", async (
            Guid messageId,
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
                await chat.MarkReadAsync(principal.UserId, messageId, cancellationToken);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });
        

        return app;
    }
}
