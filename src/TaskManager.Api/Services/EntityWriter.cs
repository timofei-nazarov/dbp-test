using System.Net.Mail;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Api;
using TaskManager.Contracts;
using TaskManager.Domain;
using TaskManager.Infrastructure.Data;
using TaskManager.Infrastructure.Services;

namespace TaskManager.Api.Services;

public sealed class EntityWriter(AppDbContext db, FileStorage storage)
{
    private static readonly string[] Roles = ["developer", "manager", "admin"];

    public async Task<User> UserAsync(RequestData data, User? user, bool partial, CancellationToken cancellationToken)
    {
        user ??= new User();
        var username = data.String("username", 150, !partial, false);
        if (username is not null)
        {
            if (!Regex.IsMatch(username, @"\A[\w.@+\-]+\z"))
                throw ApiException.Invalid("username", "Enter a valid username. This value may contain only letters, numbers, and @/./+/-/_ characters.");
            if (await db.Users.AnyAsync(x => x.Username == username && x.Id != user.Id, cancellationToken))
                throw ApiException.Invalid("username", "A user with that username already exists.");
            user.Username = username;
        }
        if (data.Has("first_name")) user.FirstName = data.String("first_name", 150)!;
        if (data.Has("last_name")) user.LastName = data.String("last_name", 150)!;
        if (data.Has("email"))
        {
            var email = data.String("email", 254)!;
            if (email.Length > 0 && (!MailAddress.TryCreate(email, out var address) || address.Address != email || !email.Contains('@')))
                throw ApiException.Invalid("email", "Enter a valid email address.");
            user.Email = email;
        }
        if (data.Has("role"))
        {
            var role = data.String("role", blank: false)!;
            if (!Roles.Contains(role)) throw ApiException.Invalid("role", $"\"{role}\" is not a valid choice.");
            user.Role = role;
        }
        if (data.Avatar is not null)
        {
            if (data.Avatar.Length == 0) throw ApiException.Invalid("avatar_picture", "The submitted file is empty.");
            if (data.Avatar.Length > 1024 * 1024) throw ApiException.Invalid("avatar_picture", "Maximum size 1048576 exceeded.");
            var extension = Path.GetExtension(data.Avatar.FileName).TrimStart('.').ToLowerInvariant();
            if (extension is not ("jpeg" or "jpg" or "png"))
                throw ApiException.Invalid("avatar_picture", $"File extension “{extension}” is not allowed. Allowed extensions are: jpeg, jpg, png.");
            user.AvatarPicture = await storage.SaveAvatarAsync(data.Avatar, cancellationToken);
        }
        else if (data.Has("avatar_picture"))
        {
            throw ApiException.Invalid("avatar_picture", data["avatar_picture"] is null
                ? "This field may not be null."
                : "The submitted data was not a file. Check the encoding type on the form.");
        }
        return user;
    }

    public async Task<Tag> TagAsync(RequestData data, Tag? tag, bool partial, CancellationToken cancellationToken)
    {
        tag ??= new Tag();
        var title = data.String("title", 100, !partial, false);
        if (title is not null) tag.Title = title;
        if (data.Has("unique_id"))
        {
            var id = data.Integer("unique_id", nullable: true);
            if (id is not null && await db.Tags.AnyAsync(x => x.UniqueId == id && x.Id != tag.Id, cancellationToken))
                throw ApiException.Invalid("unique_id", "tag with this unique id already exists.");
            tag.UniqueId = id;
        }
        return tag;
    }

    public async Task<WorkTask> TaskAsync(RequestData data, WorkTask? task, bool partial, CancellationToken cancellationToken)
    {
        var creating = task is null;
        task ??= new WorkTask();
        var title = data.String("title", 100, !partial, false);
        if (title is not null) task.Title = title;
        if (data.Has("description")) task.Description = data.String("description", nullable: true);
        if (data.Has("date_to_finish")) task.DateToFinish = data.Date("date_to_finish");
        if (data.Has("condition"))
        {
            var condition = data.String("condition", blank: false)!;
            if (!TaskStatuses.All.Contains(condition)) throw ApiException.Invalid("condition", $"\"{condition}\" is not a valid choice.");
            task.Condition = condition;
        }
        if (data.Has("priority"))
        {
            var priority = data.Integer("priority", nullable: true);
            if (priority < 0) throw ApiException.Invalid("priority", "Ensure this value is greater than or equal to 0.");
            if (priority > short.MaxValue) throw ApiException.Invalid("priority", "Ensure this value is less than or equal to 32767.");
            task.Priority = (short?)priority;
        }
        foreach (var field in new[] { "author", "executor" })
        {
            if (!data.Has(field)) continue;
            if (data[field] is null)
            {
                if (field == "author") task.Author = null;
                else task.Executor = null;
                continue;
            }
            if (data[field] is not JsonObject userData) throw ApiException.Invalid(field, "Invalid data. Expected a dictionary.");
            if (!creating) throw new ApiException(500, new { detail = "Writable nested updates are not supported." });
            var user = await UserAsync(new(userData), null, false, cancellationToken);
            if (field == "author") task.Author = user;
            else task.Executor = user;
        }
        if (data.Has("tag"))
        {
            if (data["tag"] is not JsonArray tags) throw ApiException.Invalid("tag", "Expected a list of items.");
            if (!creating) throw new ApiException(500, new { detail = "Writable nested updates are not supported." });
            foreach (var item in tags)
            {
                if (item is not JsonObject tagData) throw ApiException.Invalid("tag", "Invalid data. Expected a dictionary.");
                task.Tags.Add(await TagAsync(new(tagData), null, false, cancellationToken));
            }
        }
        return task;
    }
}
