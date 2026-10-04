namespace stock_api.Contracts;

public sealed record CustomerRequest(string Name, string? Email, string? Phone);

public sealed record CustomerResponse(int Id, string Name, string? Email, string? Phone, DateTimeOffset CreatedAt);
