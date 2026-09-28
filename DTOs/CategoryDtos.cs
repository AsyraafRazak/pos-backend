namespace pos_backend.DTOs;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive,
    int ProductCount
);

public record CreateCategoryRequest(
    string Name,
    string? Description,
    string? Icon,
    int SortOrder = 0,
    bool IsActive = true
);

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive
);

