using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterLan.Contracts;
using InterLan.Testing;
using Microsoft.AspNetCore.SignalR.Client;

static HttpClient CreateHttpClient() =>
    TestHttpClientFactory.CreateLoopback();

static void Bearer(HttpClient client, string token) =>
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

static HubConnection CreateHub(Uri baseUri, string token) =>
    new HubConnectionBuilder()
        .WithUrl(new Uri(baseUri, "/hubs/chat"), options =>
        {
            options.Headers["Authorization"] = $"Bearer {token}";
            options.HttpMessageHandlerFactory = _ => new HttpClientHandler
            {
                UseProxy = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        })
        .Build();

static async Task<(HttpClient Client, string Token, Guid UserId)> EnrollAsync(
    HttpClient ownerHttp,
    Uri baseUri,
    string username,
    string displayName)
{
    using var inviteResponse = await ownerHttp.PostAsJsonAsync(
        "/api/v1/invites",
        new { validMinutes = 30 });
    inviteResponse.EnsureSuccessStatusCode();

    var invite = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>();
    var inviteToken = invite.GetProperty("inviteToken").GetString()!;

    var enrollmentSecret =
        Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
        Convert.ToHexString(Guid.NewGuid().ToByteArray());

    var client = CreateHttpClient();
    client.BaseAddress = baseUri;

    using var joinResponse = await client.PostAsJsonAsync(
        "/api/v1/join",
        new
        {
            inviteToken,
            username,
            displayName,
            deviceName = $"{displayName} P3 Device",
            platform = "p3-smoke",
            enrollmentSecret
        });
    joinResponse.EnsureSuccessStatusCode();

    var join = await joinResponse.Content.ReadFromJsonAsync<JsonElement>();
    var requestId = join.GetProperty("requestId").GetGuid();

    using var approval = await ownerHttp.PostAsJsonAsync(
        $"/api/v1/join/{requestId:D}/decision",
        new { approve = true });
    approval.EnsureSuccessStatusCode();

    using var exchange = await client.PostAsJsonAsync(
        "/api/v1/join/exchange",
        new
        {
            requestId,
            enrollmentSecret
        });
    exchange.EnsureSuccessStatusCode();

    var session = await exchange.Content.ReadFromJsonAsync<JsonElement>();
    var token = session.GetProperty("bearerToken").GetString()!;
    var userId = session.GetProperty("userId").GetGuid();
    Bearer(client, token);

    return (client, token, userId);
}

var repositoryRoot = RepositoryLayout.FindRoot();
await using var server = await InterLanServerProcess.StartAsync(repositoryRoot);
var baseUri = server.BaseUri;

using var ownerHttp = CreateHttpClient();
ownerHttp.BaseAddress = baseUri;

using var bootstrap = await ownerHttp.PostAsJsonAsync(
    "/api/v1/bootstrap/server",
    new
    {
        serverName = "P3 Realtime Smoke",
        ownerUsername = "owner",
        ownerDisplayName = "Owner",
        ownerPassword = "p3-realtime-owner-password"
    });

if (!bootstrap.IsSuccessStatusCode)
{
    Console.Error.WriteLine(
        $"FAIL bootstrap {(int)bootstrap.StatusCode}: " +
        await bootstrap.Content.ReadAsStringAsync());
    return 1;
}

using var ownerLogin = await ownerHttp.PostAsJsonAsync(
    "/api/v1/auth/owner/login",
    new
    {
        username = "owner",
        password = "p3-realtime-owner-password"
    });
ownerLogin.EnsureSuccessStatusCode();

var ownerSession = await ownerLogin.Content.ReadFromJsonAsync<JsonElement>();
var ownerToken = ownerSession.GetProperty("bearerToken").GetString()!;
var ownerUserId = ownerSession.GetProperty("userId").GetGuid();
Bearer(ownerHttp, ownerToken);

var adminEnrollment = await EnrollAsync(
    ownerHttp,
    baseUri,
    "admin",
    "Admin");
using var adminHttp = adminEnrollment.Client;

var memberEnrollment = await EnrollAsync(
    ownerHttp,
    baseUri,
    "member",
    "Member");
using var memberHttp = memberEnrollment.Client;

using var createGroup = await ownerHttp.PostAsJsonAsync(
    "/api/v1/groups",
    new CreateGroupRequest("P3 Operations", "Realtime authority proof"));
createGroup.EnsureSuccessStatusCode();

var group = await createGroup.Content.ReadFromJsonAsync<GroupDetailsResponse>()
    ?? throw new InvalidDataException("Create group response was empty.");

if (group.MyRole != "OWNER" ||
    group.CreatedByUserId != ownerUserId ||
    group.Members.Count != 1)
{
    Console.Error.WriteLine("FAIL group creator did not become sole owner");
    return 1;
}

Console.WriteLine("PASS group creator becomes sole owner through HTTP API");

using (var forbiddenBeforeAdd = await memberHttp.GetAsync(
    $"/api/v1/groups/{group.GroupId:D}"))
{
    if (forbiddenBeforeAdd.StatusCode != HttpStatusCode.Forbidden)
    {
        Console.Error.WriteLine(
            $"FAIL non-member group read returned {(int)forbiddenBeforeAdd.StatusCode}");
        return 1;
    }
}

Console.WriteLine("PASS authenticated non-member cannot read group");

using (var addAdmin = await ownerHttp.PostAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/members",
    new AddGroupMemberRequest(adminEnrollment.UserId, "ADMIN")))
{
    addAdmin.EnsureSuccessStatusCode();
}

using (var addMember = await adminHttp.PostAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/members",
    new AddGroupMemberRequest(memberEnrollment.UserId, "MEMBER")))
{
    addMember.EnsureSuccessStatusCode();
}

Console.WriteLine("PASS owner/admin membership authority is reachable through HTTP API");

await using var ownerHub = CreateHub(baseUri, ownerToken);
await using var memberHub = CreateHub(baseUri, memberEnrollment.Token);

var typingEvent = new TaskCompletionSource<GroupTypingIndicatorResponse>(
    TaskCreationOptions.RunContinuationsAsynchronously);
var readReceiptEvent =
    new TaskCompletionSource<GroupMessageReceiptsChangedResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);

var editedEvent = new TaskCompletionSource<MessageResponse>(
    TaskCreationOptions.RunContinuationsAsynchronously);
var deletedEvent = new TaskCompletionSource<GroupMessageDeletedResponse>(
    TaskCreationOptions.RunContinuationsAsynchronously);

var memberMessageCount = 0;
var firstMessageEvent = new TaskCompletionSource<MessageResponse>(
    TaskCreationOptions.RunContinuationsAsynchronously);

ownerHub.On<GroupTypingIndicatorResponse>(
    "GroupTypingChanged",
    payload =>
    {
        if (payload.GroupId == group.GroupId &&
            payload.UserId == memberEnrollment.UserId &&
            payload.IsTyping)
        {
            typingEvent.TrySetResult(payload);
        }
    });

ownerHub.On<GroupMessageReceiptsChangedResponse>(
    "GroupReceiptUpdated",
    payload =>
    {
        if (payload.GroupId == group.GroupId &&
            payload.Receipts.Any(receipt =>
                receipt.UserId == memberEnrollment.UserId &&
                receipt.ReadUtc is not null))
        {
            readReceiptEvent.TrySetResult(payload);
        }
    });

memberHub.On<MessageResponse>(
    "MessageEdited",
    message =>
    {
        if (message.ScopeType == "GROUP" &&
            message.ScopeId == group.GroupId)
        {
            editedEvent.TrySetResult(message);
        }
    });

memberHub.On<GroupMessageDeletedResponse>(
    "GroupMessageDeleted",
    payload =>
    {
        if (payload.GroupId == group.GroupId)
            deletedEvent.TrySetResult(payload);
    });

memberHub.On<MessageResponse>(
    "MessageCreated",
    message =>
    {
        if (message.ScopeType == "GROUP" &&
            message.ScopeId == group.GroupId)
        {
            Interlocked.Increment(ref memberMessageCount);
            firstMessageEvent.TrySetResult(message);
        }
    });

await ownerHub.StartAsync();
await memberHub.StartAsync();

await memberHub.InvokeAsync(
    "SetGroupTyping",
    group.GroupId,
    true);

await typingEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
Console.WriteLine("PASS active group member typing reaches current peer set");

var clientMessageId = Guid.NewGuid();
using var send = await ownerHttp.PostAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages",
    new SendMessageRequest(clientMessageId, "durable group hello"));

if (!send.IsSuccessStatusCode)
{
    Console.Error.WriteLine(
        $"FAIL group send {(int)send.StatusCode}: " +
        await send.Content.ReadAsStringAsync());
    return 1;
}

var persisted = await send.Content.ReadFromJsonAsync<MessageResponse>()
    ?? throw new InvalidDataException("Group send response was empty.");

var realtime = await firstMessageEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (realtime.MessageId != persisted.MessageId ||
    realtime.ClientMessageId != clientMessageId)
{
    Console.Error.WriteLine("FAIL group realtime payload mismatched persisted message");
    return 1;
}

Console.WriteLine("PASS group message persists before realtime delivery");

using (var duplicate = await ownerHttp.PostAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages",
    new SendMessageRequest(clientMessageId, "durable group hello")))
{
    duplicate.EnsureSuccessStatusCode();
    var duplicateMessage =
        await duplicate.Content.ReadFromJsonAsync<MessageResponse>();

    if (duplicateMessage?.MessageId != persisted.MessageId)
    {
        Console.Error.WriteLine("FAIL idempotent HTTP retry changed group message identity");
        return 1;
    }
}

await Task.Delay(250);
if (Volatile.Read(ref memberMessageCount) != 1)
{
    Console.Error.WriteLine("FAIL idempotent group retry rebroadcast duplicate message");
    return 1;
}

Console.WriteLine("PASS idempotent group retry does not duplicate persistence or broadcast");

using (var edit = await ownerHttp.PutAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages/{persisted.MessageId:D}",
    new EditMessageRequest("durable group hello edited")))
{
    edit.EnsureSuccessStatusCode();
}

var editedRealtime = await editedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (editedRealtime.MessageId != persisted.MessageId ||
    editedRealtime.Body != "durable group hello edited" ||
    editedRealtime.EditedUtc is null)
{
    Console.Error.WriteLine("FAIL group edit realtime payload mismatch");
    return 1;
}

Console.WriteLine("PASS group sender edit persists and reaches current members");

using (var delivered = await memberHttp.PostAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages/{persisted.MessageId:D}/delivered",
    content: null))
{
    delivered.EnsureSuccessStatusCode();
}

using (var read = await memberHttp.PostAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages/{persisted.MessageId:D}/read",
    content: null))
{
    read.EnsureSuccessStatusCode();
}

await readReceiptEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
Console.WriteLine("PASS group read receipt reaches current members in realtime");

using (var delete = await ownerHttp.DeleteAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages/{persisted.MessageId:D}"))
{
    delete.EnsureSuccessStatusCode();
}

var deletedRealtime = await deletedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (deletedRealtime.MessageId != persisted.MessageId ||
    deletedRealtime.GroupId != group.GroupId)
{
    Console.Error.WriteLine("FAIL group delete realtime payload mismatch");
    return 1;
}

Console.WriteLine("PASS group sender delete persists and reaches current members");

using var removeMember = await adminHttp.DeleteAsync(
    $"/api/v1/groups/{group.GroupId:D}/members/{memberEnrollment.UserId:D}");
removeMember.EnsureSuccessStatusCode();

using (var removedHistory = await memberHttp.GetAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages?limit=100"))
{
    if (removedHistory.StatusCode != HttpStatusCode.Forbidden)
    {
        Console.Error.WriteLine(
            $"FAIL removed member retained HTTP group authority: " +
            $"{(int)removedHistory.StatusCode}");
        return 1;
    }
}

Console.WriteLine("PASS removed member loses HTTP group authority immediately");

var removedTypingRejected = false;
try
{
    await memberHub.InvokeAsync(
        "SetGroupTyping",
        group.GroupId,
        true);
}
catch
{
    removedTypingRejected = true;
}

if (!removedTypingRejected)
{
    Console.Error.WriteLine(
        "FAIL removed member retained group hub invocation authority");
    return 1;
}

Console.WriteLine("PASS removed member loses group hub authority immediately");

using var secondSend = await ownerHttp.PostAsJsonAsync(
    $"/api/v1/groups/{group.GroupId:D}/messages",
    new SendMessageRequest(Guid.NewGuid(), "post-removal group message"));
secondSend.EnsureSuccessStatusCode();

await Task.Delay(500);
if (Volatile.Read(ref memberMessageCount) != 1)
{
    Console.Error.WriteLine(
        "FAIL removed member still received group realtime delivery");
    return 1;
}

Console.WriteLine("PASS removed member is excluded from current realtime delivery targets");

using var eventResponse = await ownerHttp.GetAsync(
    $"/api/v1/groups/{group.GroupId:D}/events?limit=250");
eventResponse.EnsureSuccessStatusCode();

var events = await eventResponse.Content.ReadFromJsonAsync<GroupEventResponse[]>()
    ?? Array.Empty<GroupEventResponse>();

if (!events.Any(item =>
        item.EventType == "MEMBER_REMOVED" &&
        item.SubjectUserId == memberEnrollment.UserId))
{
    Console.Error.WriteLine("FAIL group removal missing from durable event history");
    return 1;
}

Console.WriteLine("PASS group authority change remains in durable event history");
Console.WriteLine("INTER-LAN P3 REALTIME SMOKE: PASS");
return 0;
