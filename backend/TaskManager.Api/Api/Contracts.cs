using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.Api;

public sealed record UserInput
{
    [Required, MaxLength(150)] public string Username { get; init; } = "";
    [MaxLength(150)] public string FirstName { get; init; } = "";
    [MaxLength(150)] public string LastName { get; init; } = "";
    [EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    public string Role { get; init; } = "developer";
}

public sealed record TagInput
{
    [Required, MaxLength(100)] public string Title { get; init; } = "";
    public long? UniqueId { get; init; }
}

public sealed record TaskInput
{
    [Required, MaxLength(100)] public string Title { get; init; } = "";
    public string? Description { get; init; }
    public DateOnly? DateToFinish { get; init; }
    public string Condition { get; init; } = "new_task";
    [Range(0, 32767)] public short? Priority { get; init; }
    public UserInput? Author { get; init; }
    public UserInput? Executor { get; init; }
    public TagInput[] Tag { get; init; } = [];
}

public sealed record LoginInput([property: Required] string Username, [property: Required] string Password);
public sealed record RefreshInput([property: Required] string Refresh);
public sealed record CountdownInput(int Seconds);
public sealed record UserResponse(long Id, string Username, string FirstName, string LastName, string Email, string Role, string? AvatarPicture);
public sealed record TagResponse(long Id, string Title, long? UniqueId);
public sealed record TaskResponse(long Id, string Title, string? Description, DateOnly DateCreated, DateOnly DateUpdated,
    DateOnly? DateToFinish, string Condition, short? Priority, UserResponse? Author, UserResponse? Executor, IEnumerable<TagResponse> Tag, bool IsOverdue);
