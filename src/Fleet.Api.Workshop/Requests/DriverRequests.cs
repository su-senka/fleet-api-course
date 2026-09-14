namespace Fleet.Api.Workshop.Requests;

public sealed record RegisterDriverRequest(string EmployeeNumber, string Name, string? UserId);
