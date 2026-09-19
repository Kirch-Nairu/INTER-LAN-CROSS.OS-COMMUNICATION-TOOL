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
