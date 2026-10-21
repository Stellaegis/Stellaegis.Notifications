# Stellaegis.Notifications

Stellaegis.Notifications is a .NET library that pushes notifications from an application to the subscribers
of one compartment — a tenant's front-ends, a mobile application, an agent on a customer's premises. Your
code publishes through one port, `INotificationChannel`; the adapter you register decides how a notification
travels: to the subscribers of your own process, or to the connections of a SignalR hub mapped in your host.

Three rules hold whatever the adapter. A notification always names its recipient, a compartment: there is no
broadcast, and the shared space receives nothing. Only the notification types your application declares may
leave: a type that was never declared cannot be published, by construction rather than by discipline. And a
connection receives the notifications of a compartment only after your application authorized it, server-side,
never on the compartment the client names.

The library targets .NET 10 and is released under the MIT license. It is a module of the Stellaegis family, on
[Stellaegis.Primitives](https://github.com/Stellaegis/Stellaegis.Primitives) 5.0.

## Packages

| Package | Contents | Depends on |
| --- | --- | --- |
| `Stellaegis.Notifications` | `INotificationChannel`, `Notification` and `NotificationType`, the declared types, the in-process channel and its subscribers, the module and its operation | `Stellaegis.Primitives`, `Stellaegis.Primitives.Hosting` |
| `Stellaegis.Notifications.SignalR` | The hub a subscriber connects to, the policy that authorizes a subscription, and the channel that pushes to the connections of a compartment | `Stellaegis.Primitives.AspNetCore`, ASP.NET Core |

## Publishing

Register the module and declare the types your application publishes, written `target.name` like the
operations of the family:

```csharp
builder.Services.AddStellaegisNotifications(types => types
    .Allow("invoices.batch_ready")
    .Allow("documents.ready"));
```

Then publish, naming the recipient, the type, what subscribers receive, and who sends:

```csharp
public sealed class BatchService(INotificationChannel notifications)
{
    public async Task CloseAsync(Compartment compartment, Guid batchId, Actor actor, CancellationToken ct)
    {
        // ... the batch is closed and committed ...
        await notifications.PublishAsync(new Notification
        {
            Recipient = compartment,
            Type = InvoiceNotifications.BatchReady,
            Payload = JsonSerializer.SerializeToElement(new { batchId }),
            SentBy = actor,
        }, ct);
    }
}
```

A type that was not declared fails the publication with `NotificationTypeNotAllowedException`, a
`UserFacingException` of code `NotificationType.NotAllowed`. Delivery is best effort, as the publication of an
event is: a subscriber that is not connected misses the notification, and the committed state stays the
reference. The sender is recorded for traceability and never delivered to subscribers.

A `NotificationType` is a target and a name, each following the identifier rule of the family — lowercase
letters, digits and single underscores: `invoices.batch_ready`. Uppercase and hyphens are refused, never
converted. Declare the types of a module once, as constants:

```csharp
public static class InvoiceNotifications
{
    public static readonly NotificationType BatchReady = new("invoices", "batch_ready");
}
```

## In your own process

Without a transport, the in-process channel delivers every notification to the subscribers of the process,
in registration order — a view that refreshes, a local relay. A subscriber keeps the notifications of the
compartments it serves:

```csharp
sealed class InvoiceListRefresher : INotificationSubscriber
{
    public Task ReceiveAsync(Notification notification, CancellationToken ct)
    {
        if (notification.Type == InvoiceNotifications.BatchReady)
            Refresh(notification.Recipient);
        return Task.CompletedTask;
    }
}

builder.Services.AddStellaegisNotificationSubscriber<InvoiceListRefresher>();
```

Delivery is sequential and awaited: a subscriber that throws fails the publication, and the subscribers after it
are not reached. A subscriber that must not fail the publisher catches its own failures.

## Over SignalR

A host that serves subscribers outside its process maps the hub, under its own authentication. The package
registers no authentication scheme: the hub runs under the schemes the host declares, in a single-process
application as in a dedicated host. Your application provides the policy that authorizes a subscription — its
memberships, its rights —, and the hub cannot be registered without one:

```csharp
sealed class MembershipPolicy(IMemberships memberships) : INotificationSubscriptionPolicy
{
    public Task<bool> CanSubscribeAsync(ClaimsPrincipal user, Compartment compartment, CancellationToken ct)
        => memberships.IsMemberAsync(user.FindFirst("sub")!.Value, compartment, ct);
}

builder.Services.AddStellaegisNotifications(types => types.Allow("invoices.batch_ready"));
builder.Services.AddStellaegisNotificationsHub<MembershipPolicy>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapNotificationsHub().RequireAuthorization();   // "/notifications" unless given
```

`AddStellaegisNotificationsHub` replaces the in-process channel with the one that pushes to the hub: a
notification reaches the connections subscribed to its recipient as `Receive(type, payload, occurredAt)`.

A subscriber connects, then subscribes to one compartment; the hub asks the policy, and joins the connection to
the compartment's group only when it says yes:

```csharp
var connection = new HubConnectionBuilder().WithUrl("https://api.example.com/notifications", options =>
    options.AccessTokenProvider = () => Task.FromResult<string?>(token)).Build();
connection.On<string, JsonElement, DateTimeOffset>("Receive", (type, payload, occurredAt) => ...);
await connection.StartAsync();
await connection.InvokeAsync("Subscribe", "acme");
```

A refusal, or a value that is not a compartment, answers a `HubException`. The hub belongs to the module and
performs `notifications.subscribe`, which the startup check of Stellaegis.Primitives reads as on any route: a
host that maps the hub without declaring its authorization fails to start, and so does one mapping it in the
shared space, which receives nothing.

A WebSocket connection cannot send an `Authorization` header. A host authenticating with JWT bearer tokens
lets its scheme read the token from the query string on the hub's path:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var token = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/notifications"))
            context.Token = token;
        return Task.CompletedTask;
    }
};
```

## What the module declares

`AddStellaegisNotifications` declares the module to the application, with compartments — a notification is
addressed to one —, with its operation, and compatible with an isolation by owner since it holds no data of
an actor. An application without compartments cannot use it: the validation of Stellaegis.Primitives says so
at startup. The module holds no data and declares no database.

## Building and testing

```text
dotnet build Stellaegis.Notifications.slnx -c Release
dotnet test --solution Stellaegis.Notifications.slnx -c Release
```

The tests run the hub on an in-memory test server, under a header-based authentication standing for the
host's; nothing is skipped.

## License

MIT — see [LICENSE](LICENSE). The packages reference third-party packages listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
