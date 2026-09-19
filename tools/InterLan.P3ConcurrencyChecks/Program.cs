using InterLan.Contracts;
using InterLan.Infrastructure;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

async Task<string> CaptureMutationAsync(Func<Task> mutation)
{
    try
    {
        await mutation();
        return "SUCCESS";
    }
    catch (UnauthorizedAccessException)
    {
        return "UNAUTHORIZED";
    }
    catch (KeyNotFoundException)
    {
        return "NOT_FOUND";
    }
    catch (Exception exception)
    {
        return $"ERROR:{exception.GetType().Name}:{exception.Message}";
    }
}

var root = Path.Combine(
    Path.GetTempPath(),
    "interlan-p3-concurrency-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var databasePath = Path.Combine(root, "p3-concurrency.db");
    var database = new SqliteDatabase(databasePath);
    await database.InitializeAsync();

    var ownerId = Guid.NewGuid();
    var adminId = Guid.NewGuid();
    var memberId = Guid.NewGuid();
    var lateMemberId = Guid.NewGuid();
    var sendRaceMemberId = Guid.NewGuid();
    var roleRaceMemberId = Guid.NewGuid();
    var receiptRaceMemberId = Guid.NewGuid();
    var metadataRaceAdminId = Guid.NewGuid();

    await using (var connection = database.OpenConnection())
    {
        foreach (var user in new[]
        {
            (ownerId, "p3-owner", "P3 Owner", "OWNER"),
            (adminId, "p3-admin", "P3 Admin", "MEMBER"),
            (memberId, "p3-member", "P3 Member", "MEMBER"),
            (lateMemberId, "p3-late", "P3 Late Member", "MEMBER"),
            (sendRaceMemberId, "p3-send-race", "P3 Send Race", "MEMBER"),
            (roleRaceMemberId, "p3-role-race", "P3 Role Race", "MEMBER"),
            (receiptRaceMemberId, "p3-receipt-race", "P3 Receipt Race", "MEMBER"),
            (metadataRaceAdminId, "p3-metadata-race", "P3 Metadata Race", "MEMBER")
        })
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO users (
                    user_id, username, display_name, role, created_utc
                ) VALUES (
                    $id, $username, $displayName, $role, $createdUtc
                );
                """;
            insert.Parameters.AddWithValue("$id", user.Item1.ToString("D"));
            insert.Parameters.AddWithValue("$username", user.Item2);
            insert.Parameters.AddWithValue("$displayName", user.Item3);
            insert.Parameters.AddWithValue("$role", user.Item4);
            insert.Parameters.AddWithValue(
                "$createdUtc",
                DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }
    }

    var groups = new GroupStore(database);
    var group = await groups.CreateGroupAsync(
        ownerId,
        new CreateGroupRequest("P3 Concurrency"));

    await groups.AddMemberAsync(
        ownerId,
        group.GroupId,
        new AddGroupMemberRequest(adminId, "ADMIN"));
    await groups.AddMemberAsync(
        adminId,
        group.GroupId,
        new AddGroupMemberRequest(memberId));

    var metadataRaceLinearized = true;
    for (var attempt = 0; attempt < 24; attempt++)
    {
        await groups.AddMemberAsync(
            ownerId,
            group.GroupId,
            new AddGroupMemberRequest(metadataRaceAdminId, "ADMIN"));

        var requestedName = $"P3 Metadata Race {attempt:D2}";
        var removeVsMetadata = await Task.WhenAll(
            CaptureMutationAsync(async () =>
            {
                var updated = await groups.UpdateGroupAsync(
                    metadataRaceAdminId,
                    group.GroupId,
                    new UpdateGroupRequest(requestedName, "metadata authority race"));

                if (updated.Name != requestedName)
                    throw new InvalidOperationException(
                        "Committed metadata update returned the wrong snapshot.");
            }),
            CaptureMutationAsync(async () =>
            {
                await groups.RemoveMemberAsync(
                    ownerId,
                    group.GroupId,
                    metadataRaceAdminId);
            }));

        var afterMetadataRace = await groups.GetGroupDetailsAsync(
            ownerId,
            group.GroupId);

        metadataRaceLinearized &=
            removeVsMetadata[1] == "SUCCESS" &&
            removeVsMetadata[0] is "SUCCESS" or "UNAUTHORIZED" &&
            (removeVsMetadata[0] == "SUCCESS"
                ? afterMetadataRace.Name == requestedName
                : afterMetadataRace.Name != requestedName) &&
            afterMetadataRace.Members.All(member =>
                member.UserId != metadataRaceAdminId);

        await groups.UpdateGroupAsync(
            ownerId,
            group.GroupId,
            new UpdateGroupRequest("P3 Concurrency", null));
    }

    Check(
        metadataRaceLinearized,
        "remove-vs-metadata update never commits behind an unauthorized result");

    var senders = new[] { ownerId, adminId, memberId };

    var uniqueWrites = await Task.WhenAll(
        Enumerable.Range(0, 180)
            .Select(index => groups.SendGroupMessageAsync(
                senders[index % senders.Length],
                group.GroupId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    $"group-stress-{index:D3}"))));

    Check(
        uniqueWrites.All(write => write.Created),
        "one hundred eighty concurrent group writes persist without lock loss");

    var history = await groups.GetGroupHistoryAsync(
        ownerId,
        group.GroupId,
        afterMessageId: null,
        limit: 250);

    Check(
        history.Count == 180 &&
        history.Select(message => message.MessageId).Distinct().Count() == 180,
        "concurrent group history contains every unique message exactly once");

    var duplicateClientId = Guid.NewGuid();

    var duplicateWrites = await Task.WhenAll(
        Enumerable.Range(0, 48)
            .Select(_ => groups.SendGroupMessageAsync(
                ownerId,
                group.GroupId,
                new SendMessageRequest(
                    duplicateClientId,
                    "group-duplicate-race"))));

    Check(
        duplicateWrites.Count(write => write.Created) == 1 &&
        duplicateWrites
            .Select(write => write.Message.MessageId)
            .Distinct()
            .Count() == 1,
        "forty-eight concurrent duplicate group sends collapse to one message");

    var concurrentAdds = await Task.WhenAll(
        Enumerable.Range(0, 32)
            .Select(_ => groups.AddMemberAsync(
                ownerId,
                group.GroupId,
                new AddGroupMemberRequest(lateMemberId, "MEMBER"))));

    Check(
        concurrentAdds.Count(result => result.Status == "ADDED") == 1 &&
        concurrentAdds.All(result =>
            result.Status is "ADDED" or "UNCHANGED"),
        "concurrent identical membership adds converge to one active row");

    var detailsAfterAdd = await groups.GetGroupDetailsAsync(
        lateMemberId,
        group.GroupId);

    Check(
        detailsAfterAdd.Members.Count(member =>
            member.UserId == lateMemberId) == 1,
        "concurrent membership add never duplicates group member row");

    var roleMutations = await Task.WhenAll(
        Enumerable.Range(0, 24)
            .Select(index => groups.UpdateMemberRoleAsync(
                ownerId,
                group.GroupId,
                lateMemberId,
                new UpdateGroupMemberRoleRequest(
                    index % 2 == 0 ? "ADMIN" : "MEMBER"))));

    Check(
        roleMutations.All(result =>
            result.Status is "ROLE_CHANGED" or "UNCHANGED"),
        "concurrent owner role mutations complete without authority corruption");

    var detailsAfterRoles = await groups.GetGroupDetailsAsync(
        ownerId,
        group.GroupId);
    var lateMember = detailsAfterRoles.Members.Single(member =>
        member.UserId == lateMemberId);

    Check(
        lateMember.Role is "ADMIN" or "MEMBER",
        "concurrent role mutation leaves assignable final role");

    await groups.AddMemberAsync(
        ownerId,
        group.GroupId,
        new AddGroupMemberRequest(sendRaceMemberId));
    await groups.AddMemberAsync(
        ownerId,
        group.GroupId,
        new AddGroupMemberRequest(roleRaceMemberId));

    var removeVsSendClientId = Guid.NewGuid();
    var removeVsSend = await Task.WhenAll(
        CaptureMutationAsync(async () =>
        {
            await groups.SendGroupMessageAsync(
                sendRaceMemberId,
                group.GroupId,
                new SendMessageRequest(
                    removeVsSendClientId,
                    "remove-vs-send-race"));
        }),
        CaptureMutationAsync(async () =>
        {
            await groups.RemoveMemberAsync(
                ownerId,
                group.GroupId,
                sendRaceMemberId);
        }));

    Check(
        removeVsSend[1] == "SUCCESS" &&
        removeVsSend[0] is "SUCCESS" or "UNAUTHORIZED",
        "remove-vs-send race linearizes without lock or authority failure");

    var postRemovalSend = await CaptureMutationAsync(async () =>
    {
        await groups.SendGroupMessageAsync(
            sendRaceMemberId,
            group.GroupId,
            new SendMessageRequest(
                Guid.NewGuid(),
                "post-removal-send"));
    });

    Check(
        postRemovalSend == "UNAUTHORIZED",
        "remove-vs-send race leaves member immediately unable to send");

    var removeVsRole = await Task.WhenAll(
        CaptureMutationAsync(async () =>
        {
            await groups.UpdateMemberRoleAsync(
                ownerId,
                group.GroupId,
                roleRaceMemberId,
                new UpdateGroupMemberRoleRequest("ADMIN"));
        }),
        CaptureMutationAsync(async () =>
        {
            await groups.RemoveMemberAsync(
                ownerId,
                group.GroupId,
                roleRaceMemberId);
        }));

    Check(
        removeVsRole[1] == "SUCCESS" &&
        removeVsRole[0] is "SUCCESS" or "NOT_FOUND",
        "remove-vs-role race linearizes without lock or authority failure");

    var roleAfterRemoval = await CaptureMutationAsync(async () =>
    {
        await groups.UpdateMemberRoleAsync(
            ownerId,
            group.GroupId,
            roleRaceMemberId,
            new UpdateGroupMemberRoleRequest("ADMIN"));
    });

    Check(
        roleAfterRemoval == "NOT_FOUND",
        "removed member cannot be role-mutated after the race");

    await groups.AddMemberAsync(
        ownerId,
        group.GroupId,
        new AddGroupMemberRequest(receiptRaceMemberId));

    var receiptRaceMessage = history[0];
    var removeVsReceipt = await Task.WhenAll(
        CaptureMutationAsync(async () =>
        {
            await groups.MarkGroupMessageReadAsync(
                receiptRaceMemberId,
                group.GroupId,
                receiptRaceMessage.MessageId);
        }),
        CaptureMutationAsync(async () =>
        {
            await groups.RemoveMemberAsync(
                ownerId,
                group.GroupId,
                receiptRaceMemberId);
        }));

    Check(
        removeVsReceipt[1] == "SUCCESS" &&
        removeVsReceipt[0] is "SUCCESS" or "UNAUTHORIZED",
        "remove-vs-receipt race linearizes without lock or authority failure");

    var postRemovalReceipt = await CaptureMutationAsync(async () =>
    {
        await groups.MarkGroupMessageDeliveredAsync(
            receiptRaceMemberId,
            group.GroupId,
            receiptRaceMessage.MessageId);
    });

    Check(
        postRemovalReceipt == "UNAUTHORIZED",
        "remove-vs-receipt race leaves member unable to mutate receipts");

    var deliveryTargets = await groups.GetGroupDeliveryTargetUserIdsAsync(
        group.GroupId);

    Check(
        !deliveryTargets.Contains(sendRaceMemberId) &&
        !deliveryTargets.Contains(roleRaceMemberId) &&
        !deliveryTargets.Contains(receiptRaceMemberId),
        "delivery target projection excludes all members removed by authority races");

    var receiptTargets = history
        .Take(120)
        .ToArray();

    await Task.WhenAll(
        receiptTargets.Select(message =>
            groups.MarkGroupMessageReadAsync(
                message.SenderUserId == memberId ? adminId : memberId,
                group.GroupId,
                message.MessageId)));

    var receiptProof = await groups.GetGroupMessageReceiptsAsync(
        ownerId,
        group.GroupId,
        receiptTargets[0].MessageId);

    Check(
        receiptProof.Count == 1 &&
        receiptProof[0].ReadUtc is not null &&
        receiptProof[0].DeliveredUtc is not null,
        "concurrent group receipt writes retain monotonic read state");

    await groups.RemoveMemberAsync(
        ownerId,
        group.GroupId,
        lateMemberId);

    var concurrentRestores = await Task.WhenAll(
        Enumerable.Range(0, 32)
            .Select(_ => groups.AddMemberAsync(
                ownerId,
                group.GroupId,
                new AddGroupMemberRequest(lateMemberId, "MEMBER"))));

    Check(
        concurrentRestores.Count(result => result.Status == "ADDED") == 1 &&
        concurrentRestores.All(result =>
            result.Status is "ADDED" or "UNCHANGED"),
        "concurrent identical membership restores converge to one active row");

    await groups.RemoveMemberAsync(
        ownerId,
        group.GroupId,
        lateMemberId);

    var removedSendRejected = false;
    try
    {
        await groups.SendGroupMessageAsync(
            lateMemberId,
            group.GroupId,
            new SendMessageRequest(
                Guid.NewGuid(),
                "removed-member-send"));
    }
    catch (UnauthorizedAccessException)
    {
        removedSendRejected = true;
    }

    Check(
        removedSendRejected,
        "removed member cannot send after concurrent authority workload");

    var reopenedDatabase = new SqliteDatabase(databasePath);
    await reopenedDatabase.InitializeAsync();
    var reopenedGroups = new GroupStore(reopenedDatabase);

    var restartedHistory = await reopenedGroups.GetGroupHistoryAsync(
        ownerId,
        group.GroupId,
        afterMessageId: null,
        limit: 250);

    Check(
        restartedHistory.Count == 181,
        "concurrent group workload survives database reopen");

    var restartedDetails = await reopenedGroups.GetGroupDetailsAsync(
        ownerId,
        group.GroupId);

    Check(
        restartedDetails.Members.All(member =>
            member.UserId != lateMemberId &&
            member.UserId != sendRaceMemberId &&
            member.UserId != roleRaceMemberId &&
            member.UserId != receiptRaceMemberId),
        "removed memberships remain revoked after database reopen");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(
        $"P3 concurrency checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN P3 CONCURRENCY CHECKS: PASS");
return 0;
