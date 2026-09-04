namespace Stepwise.Core.Models;

/// <summary>
/// Доменная модель проекта руководства (Guide Project).
/// </summary>
public sealed record Project(
    Guid Id,
    string Name,
    string RootPath,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<Step>? Steps = null,
    string? Description = null
);
