using InterLan.Contracts;
using InterLan.Infrastructure;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

async Task ExpectUnauthorizedAsync(Func<Task> action, string name)
{
    try
    {
        await action();
        Check(false, name);
    }
    catch (UnauthorizedAccessException)
    {
        Check(true, name);
    }
}

async Task ExpectArgumentAsync(Func<Task> action, string name)
{
    try
    {
        await action();
        Check(false, name);
    }
    catch (ArgumentException)
    {
        Check(true, name);
    }
}

async Task ExpectInvalidOperationAsync(Func<Task> action, string name)
{
    try
    {
        await action();
        Check(false, name);
    }
    catch (InvalidOperationException)
    {
        Check(true, name);
    }
}

var root = Path.Combine(
    Path.GetTempPath(),
    "interlan-p3-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var databasePath = Path.Combine(root, "interlan.db");
    var database = new SqliteDatabase(databasePath);
    await database.InitializeAsync();

    var ownerId = Guid.NewGuid();
    var adminId = Guid.NewGuid();
    var memberId = Guid.NewGuid();
    var secondAdminId = Guid.NewGuid();

    await using (var connection = database.OpenConnection())
    {
        foreach (var user in new[]
        {
            (ownerId, "owner", "Owner", "OWNER"),
            (adminId, "admin", "Admin Candidate", "MEMBER"),
            (memberId, "member", "Member Candidate", "MEMBER"),
            (secondAdminId, "second-admin", "Second Admin Candidate", "MEMBER")
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO users (
                    user_id, username, display_name, role, created_utc
                ) VALUES (
                    $id, $username, $displayName, $role, $createdUtc
                );
                """;
            command.Parameters.AddWithValue("$id", user.Item1.ToString("D"));
            command.Parameters.AddWithValue("$username", user.Item2);
            command.Parameters.AddWithValue("$displayName", user.Item3);
            command.Parameters.AddWithValue("$role", user.Item4);
            command.Parameters.AddWithValue(
                "$createdUtc",
                DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
    }

    var groups = new GroupStore(database);
    var chat = new ChatStore(database);

    var created = await groups.CreateGroupAsync(
        ownerId,
        new CreateGroupRequest("Operations", "P3 authority checks"));

    Check(
        created.MyRole == "OWNER" &&
        created.Members.Count == 1 &&
        created.Members[0].UserId == ownerId &&
        created.Members[0].Role == "OWNER",
        "group creator becomes the sole owner");

    var adminAdded = await groups.AddMemberAsync(
        ownerId,
        created.GroupId,
        new AddGroupMemberRequest(adminId, "ADMIN"));

    Check(
        adminAdded.Status == "ADDED" && adminAdded.Role == "ADMIN",
        "owner can add an admin");

    var memberAdded = await groups.AddMemberAsync(
        adminId,
        created.GroupId,
        new AddGroupMemberRequest(memberId));

    Check(
        memberAdded.Status == "ADDED" && memberAdded.Role == "MEMBER",
        "admin can add a member");

    var adminUpdated = await groups.UpdateGroupAsync(
        adminId,
        created.GroupId,
        new UpdateGroupRequest("  Operations Core  ", "  Active P3 topic  "));

    Check(
        adminUpdated.Name == "Operations Core" &&
        adminUpdated.Topic == "Active P3 topic",
        "group admin can update normalized group metadata");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.UpdateGroupAsync(
                memberId,
                created.GroupId,
                new UpdateGroupRequest("Member Override", null));
        },
        "group member cannot mutate group metadata");

    var duplicateMember = await groups.AddMemberAsync(
        adminId,
        created.GroupId,
        new AddGroupMemberRequest(memberId));

    Check(
        duplicateMember.Status == "UNCHANGED",
        "duplicate active membership add is idempotent");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.AddMemberAsync(
                adminId,
                created.GroupId,
                new AddGroupMemberRequest(secondAdminId, "ADMIN"));
        },
        "admin cannot create another admin");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.AddMemberAsync(
                memberId,
                created.GroupId,
                new AddGroupMemberRequest(secondAdminId));
        },
        "member cannot add another member");

    await ExpectArgumentAsync(
        async () =>
        {
            await groups.AddMemberAsync(
                ownerId,
                created.GroupId,
                new AddGroupMemberRequest(secondAdminId, "OWNER"));
        },
        "owner role cannot be assigned through member-add mutation");

    var secondAdminAdded = await groups.AddMemberAsync(
        ownerId,
        created.GroupId,
        new AddGroupMemberRequest(secondAdminId, "ADMIN"));

    Check(
        secondAdminAdded.Role == "ADMIN",
        "owner can establish multiple admins");

    var promoted = await groups.UpdateMemberRoleAsync(
        ownerId,
        created.GroupId,
        memberId,
        new UpdateGroupMemberRoleRequest("ADMIN"));

    Check(
        promoted.Status == "ROLE_CHANGED" && promoted.Role == "ADMIN",
        "owner can promote member to admin");

    var demoted = await groups.UpdateMemberRoleAsync(
        ownerId,
        created.GroupId,
        memberId,
        new UpdateGroupMemberRoleRequest("MEMBER"));

    Check(
        demoted.Status == "ROLE_CHANGED" && demoted.Role == "MEMBER",
        "owner can demote admin to member");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.UpdateMemberRoleAsync(
                adminId,
                created.GroupId,
                memberId,
                new UpdateGroupMemberRoleRequest("ADMIN"));
        },
        "admin cannot promote members");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.RemoveMemberAsync(
                adminId,
                created.GroupId,
                secondAdminId);
        },
        "admin cannot remove another admin");

    var removed = await groups.RemoveMemberAsync(
        adminId,
        created.GroupId,
        memberId);

    Check(
        removed.Status == "REMOVED" && removed.Role is null,
        "admin can remove a member");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.GetGroupDetailsAsync(memberId, created.GroupId);
        },
        "removed member immediately loses group read authority");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                memberId,
                created.GroupId,
                new SendMessageRequest(Guid.NewGuid(), "should be rejected"));
        },
        "removed member immediately loses group send authority");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.RemoveMemberAsync(ownerId, created.GroupId, ownerId);
        },
        "group owner cannot remove owner membership");

    var restored = await groups.AddMemberAsync(
        ownerId,
        created.GroupId,
        new AddGroupMemberRequest(memberId));

    Check(
        restored.Status == "ADDED" && restored.Role == "MEMBER",
        "removed membership can be restored without duplicate row");

    var directConversation = await chat.GetOrCreateDirectConversationAsync(
        ownerId,
        adminId);
    var crossScopeClientMessageId = Guid.NewGuid();

    await chat.SendDirectMessageAsync(
        ownerId,
        directConversation.ConversationId,
        new SendMessageRequest(
            crossScopeClientMessageId,
            "direct scope owns this idempotency key"));

    await ExpectInvalidOperationAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                ownerId,
                created.GroupId,
                new SendMessageRequest(
                    crossScopeClientMessageId,
                    "group scope cannot reuse it"));
        },
        "group send rejects client message ID already consumed in direct scope");

    var normalizedGroupMessage = await groups.SendGroupMessageAsync(
        adminId,
        created.GroupId,
        new SendMessageRequest(Guid.NewGuid(), "  Cafe\u0301 group  "));

    Check(
        normalizedGroupMessage.Message.Body == "Café group",
        "group message text is trimmed and normalized to Unicode NFC");

    await ExpectArgumentAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                adminId,
                created.GroupId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    "unsafe\u0000group"));
        },
        "group message rejects unsupported control characters");

    await ExpectArgumentAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                adminId,
                created.GroupId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    new string('x', GroupStore.MaxMessageLength + 1)));
        },
        "group message rejects oversized body");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.EditGroupMessageAsync(
                memberId,
                created.GroupId,
                normalizedGroupMessage.Message.MessageId,
                new EditMessageRequest("member edit"));
        },
        "non-sender cannot edit group message");

    var editedGroupMessage = await groups.EditGroupMessageAsync(
        adminId,
        created.GroupId,
        normalizedGroupMessage.Message.MessageId,
        new EditMessageRequest("  edited group message  "));

    Check(
        editedGroupMessage.Body == "edited group message" &&
        editedGroupMessage.EditedUtc is not null &&
        editedGroupMessage.DeletedUtc is null,
        "group sender can edit durable message");

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.DeleteGroupMessageAsync(
                memberId,
                created.GroupId,
                normalizedGroupMessage.Message.MessageId);
        },
        "non-sender cannot delete group message");

    var deletedGroupMessage = await groups.DeleteGroupMessageAsync(
        adminId,
        created.GroupId,
        normalizedGroupMessage.Message.MessageId);
    var duplicateDelete = await groups.DeleteGroupMessageAsync(
        adminId,
        created.GroupId,
        normalizedGroupMessage.Message.MessageId);

    Check(
        duplicateDelete.DeletedUtc == deletedGroupMessage.DeletedUtc,
        "group message delete is idempotent");

    var tombstone = await groups.GetGroupMessageByIdAsync(
        ownerId,
        created.GroupId,
        normalizedGroupMessage.Message.MessageId);

    Check(
        tombstone.Body.Length == 0 &&
        tombstone.DeletedUtc == deletedGroupMessage.DeletedUtc,
        "deleted group message remains an ordered durable tombstone");

    await ExpectArgumentAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                ownerId,
                created.GroupId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    "reply to deleted target",
                    normalizedGroupMessage.Message.MessageId));
        },
        "group reply cannot target deleted message");

    var firstClientMessageId = Guid.NewGuid();
    var firstMessage = await groups.SendGroupMessageAsync(
        ownerId,
        created.GroupId,
        new SendMessageRequest(firstClientMessageId, "group message one"));

    Check(
        firstMessage.Created &&
        firstMessage.Message.ScopeType == "GROUP" &&
        firstMessage.Message.ScopeId == created.GroupId,
        "authorized group message persists in group scope");

    var duplicateMessage = await groups.SendGroupMessageAsync(
        ownerId,
        created.GroupId,
        new SendMessageRequest(firstClientMessageId, "group message one"));

    Check(
        !duplicateMessage.Created &&
        duplicateMessage.Message.MessageId == firstMessage.Message.MessageId,
        "duplicate group client message ID is idempotent");

    await ExpectInvalidOperationAsync(
        async () =>
        {
            await groups.SendGroupMessageAsync(
                ownerId,
                created.GroupId,
                new SendMessageRequest(firstClientMessageId, "conflicting replay"));
        },
        "conflicting group idempotency replay is rejected");

    var reply = await groups.SendGroupMessageAsync(
        memberId,
        created.GroupId,
        new SendMessageRequest(
            Guid.NewGuid(),
            "group reply",
            firstMessage.Message.MessageId));

    Check(
        reply.Created &&
        reply.Message.ReplyToMessageId == firstMessage.Message.MessageId,
        "group reply targets active message in same group");

    await groups.MarkGroupMessageDeliveredAsync(
        memberId,
        created.GroupId,
        firstMessage.Message.MessageId);

    var deliveredReceipts = await groups.GetGroupMessageReceiptsAsync(
        ownerId,
        created.GroupId,
        firstMessage.Message.MessageId);

    Check(
        deliveredReceipts.Count == 1 &&
        deliveredReceipts[0].UserId == memberId &&
        deliveredReceipts[0].DeliveredUtc is not null &&
        deliveredReceipts[0].ReadUtc is null,
        "group delivery receipt records actual recipient acknowledgement");

    await groups.MarkGroupMessageReadAsync(
        memberId,
        created.GroupId,
        firstMessage.Message.MessageId);

    var readReceipts = await groups.GetGroupMessageReceiptsAsync(
        adminId,
        created.GroupId,
        firstMessage.Message.MessageId);

    Check(
        readReceipts.Count == 1 &&
        readReceipts[0].UserId == memberId &&
        readReceipts[0].DeliveredUtc is not null &&
        readReceipts[0].ReadUtc is not null &&
        readReceipts[0].ReadUtc >= readReceipts[0].DeliveredUtc,
        "group read receipt preserves monotonic delivery state");

    await ExpectInvalidOperationAsync(
        async () =>
        {
            await groups.MarkGroupMessageReadAsync(
                ownerId,
                created.GroupId,
                firstMessage.Message.MessageId);
        },
        "group sender cannot acknowledge own message");

    await groups.RemoveMemberAsync(
        adminId,
        created.GroupId,
        memberId);

    await ExpectUnauthorizedAsync(
        async () =>
        {
            await groups.MarkGroupMessageDeliveredAsync(
                memberId,
                created.GroupId,
                firstMessage.Message.MessageId);
        },
        "removed member cannot mutate group receipt state");

    await groups.AddMemberAsync(
        ownerId,
        created.GroupId,
        new AddGroupMemberRequest(memberId));

    var page = await groups.GetGroupHistoryPageAsync(
        adminId,
        created.GroupId,
        afterMessageId: normalizedGroupMessage.Message.MessageId,
        limit: 1);

    Check(
        page.Items.Count == 1 &&
        page.Items[0].MessageId == firstMessage.Message.MessageId &&
        page.HasMore &&
        page.NextAfterMessageId == firstMessage.Message.MessageId,
        "group history exposes deterministic forward cursor page");

    var catchupPage = await groups.GetGroupHistoryPageAsync(
        adminId,
        created.GroupId,
        firstMessage.Message.MessageId,
        limit: 10);

    Check(
        catchupPage.Items.Count == 1 &&
        catchupPage.Items[0].MessageId == reply.Message.MessageId &&
        !catchupPage.HasMore,
        "group history cursor catches up without duplicate messages");

    var events = await groups.ListGroupEventsAsync(
        ownerId,
        created.GroupId,
        limit: 250);

    Check(
        events.Count >= 8 &&
        events[0].EventType == "GROUP_CREATED" &&
        events.Zip(events.Skip(1), (left, right) =>
                left.CreatedUtc < right.CreatedUtc ||
                left.CreatedUtc == right.CreatedUtc &&
                string.CompareOrdinal(
                    left.GroupEventId.ToString("D"),
                    right.GroupEventId.ToString("D")) <= 0)
            .All(value => value),
        "group authority events remain durably ordered");

    Check(
        events.Any(item =>
            item.EventType == "GROUP_METADATA_UPDATED" &&
            item.ActorUserId == adminId) &&
        events.Any(item =>
            item.EventType == "MEMBER_ROLE_CHANGED" &&
            item.SubjectUserId == memberId) &&
        events.Any(item =>
            item.EventType == "MEMBER_REMOVED" &&
            item.SubjectUserId == memberId),
        "group event history records metadata and authority mutations");

    await using (var auditConnection = database.OpenConnection())
    {
        await using var audit = auditConnection.CreateCommand();
        audit.CommandText =
            """
            SELECT event_type
            FROM audit_events
            WHERE subject_type = 'GROUP'
              AND subject_id = $groupId
            ORDER BY created_utc, audit_event_id;
            """;
        audit.Parameters.AddWithValue("$groupId", created.GroupId.ToString("D"));

        var auditTypes = new List<string>();
        await using var auditReader = await audit.ExecuteReaderAsync();
        while (await auditReader.ReadAsync())
            auditTypes.Add(auditReader.GetString(0));

        Check(
            auditTypes.Contains("GROUP_CREATED") &&
            auditTypes.Contains("GROUP_METADATA_UPDATED") &&
            auditTypes.Contains("GROUP_MEMBER_ADDED") &&
            auditTypes.Contains("GROUP_MEMBER_REMOVED") &&
            auditTypes.Contains("GROUP_MEMBER_ROLE_CHANGED"),
            "group authority mutations persist in audit ledger");
    }

    var restartedDatabase = new SqliteDatabase(databasePath);
    await restartedDatabase.InitializeAsync();
    var restartedGroups = new GroupStore(restartedDatabase);

    var afterRestart = await restartedGroups.GetGroupDetailsAsync(
        memberId,
        created.GroupId);

    Check(
        afterRestart.Members.Any(member =>
            member.UserId == ownerId && member.Role == "OWNER") &&
        afterRestart.Members.Any(member =>
            member.UserId == adminId && member.Role == "ADMIN") &&
        afterRestart.Members.Any(member =>
            member.UserId == memberId && member.Role == "MEMBER"),
        "group membership authority survives restart");

    var restartHistory = await restartedGroups.GetGroupHistoryAsync(
        memberId,
        created.GroupId,
        afterMessageId: null,
        limit: 100);

    Check(
        restartHistory.Count == 3 &&
        restartHistory[0].MessageId == normalizedGroupMessage.Message.MessageId &&
        restartHistory[1].MessageId == firstMessage.Message.MessageId &&
        restartHistory[2].MessageId == reply.Message.MessageId,
        "group message history survives restart in deterministic order");

    var ownerGroups = await restartedGroups.ListGroupsAsync(ownerId);
    Check(
        ownerGroups.Any(group => group.GroupId == created.GroupId),
        "group directory survives restart");

    if (failures.Count > 0)
    {
        Console.Error.WriteLine(
            $"INTER-LAN P3 CHECKS: FAIL ({failures.Count})");
        return 1;
    }

    Console.WriteLine("INTER-LAN P3 CHECKS: PASS");
    return 0;
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}
