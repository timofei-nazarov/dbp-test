using TaskManager.Contracts;
using TaskManager.Domain;
using TaskManager.Infrastructure.Services;

namespace TaskManager.Api.Responses;

public sealed class Representations(FileStorage storage, IHttpContextAccessor accessor, TimeProvider clock)
{
    public UserResponse User(User user) => new(user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role,
        user.AvatarPicture is null ? null : Absolute(storage.PublicUrl(user.AvatarPicture)));

    public static TagResponse Tag(Tag tag) => new(tag.Id, tag.Title, tag.UniqueId);

    public TaskResponse Task(WorkTask task)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var overdueDays = TaskRules.OverdueDays(task.DateToFinish, task.Condition, today);
        return new(task.Id, task.Title, task.Description, task.DateCreated, task.DateUpdated,
        task.DateToFinish, task.Condition, task.Priority, task.Author is null ? null : User(task.Author),
        task.Executor is null ? null : User(task.Executor), task.Tags.OrderBy(x => x.Id).Select(Tag), overdueDays is not null, overdueDays);
    }

    public static object Job(BackgroundJob job)
    {
        var result = new Dictionary<string, object?> { ["task_id"] = job.Id.ToString(), ["status"] = job.Status };
        if (job.Error is not null) result["errors"] = new { non_field_errors = new[] { job.Error } };
        if (job.Result is not null) result["result"] = job.Result;
        return result;
    }

    public string Absolute(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return path;
        var request = accessor.HttpContext!.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }
}
