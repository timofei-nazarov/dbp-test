using TaskManager.Contracts;

namespace TaskManager.Domain;

public sealed class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "developer";
    public bool IsStaff { get; set; }
    public bool IsSuperuser { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime DateJoined { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? AvatarPicture { get; set; }
}

public sealed class Tag
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public long? UniqueId { get; set; }
    public List<WorkTask> Tasks { get; set; } = [];
}

public sealed class WorkTask
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly DateCreated { get; set; }
    public DateOnly DateUpdated { get; set; }
    public DateOnly? DateToFinish { get; set; }
    public string Condition { get; set; } = TaskStatuses.NewTask;
    public short? Priority { get; set; }
    public long? AuthorId { get; set; }
    public User? Author { get; set; }
    public long? ExecutorId { get; set; }
    public User? Executor { get; set; }
    public List<Tag> Tags { get; set; } = [];
}

public sealed class BackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "countdown";
    public string Payload { get; set; } = "{}";
    public string Status { get; set; } = "started";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseOwner { get; set; }
}
