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
        app.MapPost("/api/v1/realtime/ticket", async (
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeTicketStore tickets,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                return Results.Ok(tickets.Issue(
                    principal,
                    TimeSpan.FromSeconds(45)));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        }).RequireRateLimiting("auth");
        
        app.MapGet("/api/v1/presence", async (
            HttpContext context,
            EnrollmentStore enrollment,
            RealtimeConnectionRegistry connections,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                var observedUtc = DateTimeOffset.UtcNow;
                return Results.Ok(
                    connections.GetOnlineUserIds()
                        .Select(userId => new UserPresenceResponse(
                            userId,
                            IsOnline: true,
                            connections.GetUserConnectionCount(userId),
                            observedUtc))
                        .ToArray());
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        app.MapGet("/api/v1/users/search", async (
            string q,
            int? limit,
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

                return Results.Ok(await chat.SearchUsersAsync(
                    principal.UserId,
                    q,
                    Math.Clamp(limit ?? 25, 1, 100),
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
        });

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
        
        app.MapGet("/api/v1/direct/unread", async (
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

                return Results.Ok(await chat.GetDirectUnreadSummaryAsync(
                    principal.UserId,
                    cancellationToken));
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
        
        app.MapGet("/api/v1/direct/activity", async (
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

                return Results.Ok(await chat.ListDirectConversationActivityAsync(
                    principal.UserId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapGet("/api/v1/direct/{conversationId:guid}/preference", async (
            Guid conversationId,
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

                return Results.Ok(await chat.GetDirectConversationPreferenceAsync(
                    principal.UserId,
                    conversationId,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });
        
        app.MapPut("/api/v1/direct/{conversationId:guid}/preference", async (
            Guid conversationId,
            UpdateDirectConversationPreferenceRequest request,
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

                return Results.Ok(await chat.UpdateDirectConversationPreferenceAsync(
                    principal.UserId,
                    conversationId,
                    request,
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
        
        app.MapGet("/api/v1/direct/{conversationId:guid}/messages/recent", async (
            Guid conversationId,
            Guid? beforeMessageId,
            int? limit,
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

                return Results.Ok(await chat.GetDirectRecentHistoryPageAsync(
                    principal.UserId,
                    conversationId,
                    beforeMessageId,
                    Math.Clamp(limit ?? 50, 1, ChatStore.MaxMessagePageSize),
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
        
        app.MapGet("/api/v1/direct/{conversationId:guid}/messages/page", async (
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
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                return Results.Ok(await chat.GetDirectHistoryPageAsync(
                    principal.UserId,
                    conversationId,
                    afterMessageId,
                    Math.Clamp(limit ?? 50, 1, ChatStore.MaxMessagePageSize),
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
        
        app.MapGet("/api/v1/direct/{conversationId:guid}/search", async (
            Guid conversationId,
            string q,
            int? limit,
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

                return Results.Ok(await chat.SearchDirectMessagesAsync(
                    principal.UserId,
                    conversationId,
                    q,
                    Math.Clamp(limit ?? 50, 1, 100),
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
        });

        app.MapGet("/api/v1/direct/{conversationId:guid}/messages/{messageId:guid}", async (
            Guid conversationId,
            Guid messageId,
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

                return Results.Ok(await chat.GetDirectMessageByIdAsync(
                    principal.UserId,
                    conversationId,
                    messageId,
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

        app.MapPut("/api/v1/direct/{conversationId:guid}/messages/{messageId:guid}", async (
            Guid conversationId,
            Guid messageId,
            HttpContext context,
            EditMessageRequest request,
            EnrollmentStore enrollment,
            ChatStore chat,
            IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                var edited = await chat.EditDirectMessageAsync(
                    principal.UserId,
                    conversationId,
                    messageId,
                    request,
                    cancellationToken);

                var members = await chat.GetDirectMemberIdsAsync(
                    principal.UserId,
                    conversationId,
                    cancellationToken);

                foreach (var memberId in members)
                {
                    await hub.Clients.Group(ChatHub.UserGroup(memberId))
                        .SendAsync("MessageEdited", edited, cancellationToken);
                }

                return Results.Ok(edited);
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
        
        app.MapDelete("/api/v1/direct/{conversationId:guid}/messages/{messageId:guid}", async (
            Guid conversationId,
            Guid messageId,
            HttpContext context,
            EnrollmentStore enrollment,
            ChatStore chat,
            IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                var deleted = await chat.DeleteDirectMessageAsync(
                    principal.UserId,
                    conversationId,
                    messageId,
                    cancellationToken);

                var members = await chat.GetDirectMemberIdsAsync(
                    principal.UserId,
                    conversationId,
                    cancellationToken);

                foreach (var memberId in members)
                {
                    await hub.Clients.Group(ChatHub.UserGroup(memberId))
                        .SendAsync("MessageDeleted", deleted, cancellationToken);
                }

                return Results.Ok(deleted);
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
        }).RequireRateLimiting("message");
        
        app.MapPost("/api/v1/direct/{conversationId:guid}/read", async (
            Guid conversationId,
            MarkConversationReadRequest request,
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

                return Results.Ok(await chat.MarkConversationReadAsync(
                    principal.UserId,
                    conversationId,
                    request.UpToMessageId,
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
            IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                await chat.MarkDeliveredAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var conversationId = await chat.GetDirectConversationIdForMessageAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var receipts = await chat.GetReceiptsAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var payload = new MessageReceiptsChangedResponse(
                    conversationId,
                    messageId,
                    receipts);

                var members = await chat.GetDirectMemberIdsAsync(
                    principal.UserId,
                    conversationId,
                    cancellationToken);

                foreach (var memberId in members)
                {
                    await hub.Clients.Group(ChatHub.UserGroup(memberId))
                        .SendAsync("ReceiptUpdated", payload, cancellationToken);
                }

                return Results.Ok(payload);
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
            IHubContext<ChatHub> hub,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(
                    context,
                    enrollment,
                    cancellationToken);

                await chat.MarkReadAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var conversationId = await chat.GetDirectConversationIdForMessageAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var receipts = await chat.GetReceiptsAsync(
                    principal.UserId,
                    messageId,
                    cancellationToken);

                var payload = new MessageReceiptsChangedResponse(
                    conversationId,
                    messageId,
                    receipts);

                var members = await chat.GetDirectMemberIdsAsync(
                    principal.UserId,
                    conversationId,
                    cancellationToken);

                foreach (var memberId in members)
                {
                    await hub.Clients.Group(ChatHub.UserGroup(memberId))
                        .SendAsync("ReceiptUpdated", payload, cancellationToken);
                }

                return Results.Ok(payload);
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
