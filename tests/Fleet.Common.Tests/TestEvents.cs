using Fleet.Common.Messaging;

namespace Fleet.Common.Tests;

/// <summary>A stand-in event, so these tests do not depend on any module's contracts.</summary>
public sealed record SomethingHappened(string What, int Count) : IntegrationEvent;

/// <summary>A second one, to prove handlers are matched by type and not merely by being present.</summary>
public sealed record SomethingElseHappened(string What) : IntegrationEvent;
