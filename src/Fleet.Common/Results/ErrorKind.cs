namespace Fleet.Common.Results;

/// <summary>
/// The kinds of failure an application service can report.
/// </summary>
/// <remarks>
/// This enum is deliberately HTTP-agnostic. There is no <c>ErrorKind.BadRequest</c> and no
/// <c>ErrorKind.PreconditionFailed</c>, because a module has no idea it is being called over
/// HTTP. Translating these five values into status codes is the API layer's job - and, for
/// most of this course, yours.
/// </remarks>
public enum ErrorKind
{
    /// <summary>The thing you asked for does not exist.</summary>
    NotFound = 1,

    /// <summary>The request is well-formed but conflicts with the current state of the system.</summary>
    Conflict = 2,

    /// <summary>The request itself is invalid: missing fields, bad ranges, impossible combinations.</summary>
    Validation = 3,

    /// <summary>The caller is known but is not allowed to do this.</summary>
    Forbidden = 4,

    /// <summary>A dependency we do not control let us down. Retrying later might work.</summary>
    Unavailable = 5,
}
