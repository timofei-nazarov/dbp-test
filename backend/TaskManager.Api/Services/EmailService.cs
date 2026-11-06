using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using TaskManager.Api.Data;

namespace TaskManager.Api.Services;

public sealed record EmailMessage(string Subject, string Html, string[] Recipients);

public sealed class EmailService(AppDbContext db, IConfiguration configuration, ILogger<EmailService> logger)
{
    public async Task SendAssignmentAsync(long taskId, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.Include(x => x.Executor).SingleAsync(x => x.Id == taskId, cancellationToken);
        if (task.Executor is null) throw new InvalidOperationException("The task has no executor.");
        var title = WebUtility.HtmlEncode(task.Title);
        var html = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"></head><body style=\"background-color:#F4F6FA;padding:20px\"><div style=\"max-width:700px;background-color:#FFF;padding:3%\"><h1 style=\"font-size:20px\">You have been assigned a task: {title} </h1><hr><p>Some text about project.</p><a href=\"https://example.com/\">Learn More</a><p>© 2021 Project name</p></div></body></html>";
        await SendAsync(new("You've assigned a task.", html, [task.Executor.Email]), cancellationToken);
    }

    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(configuration["Email:From"] ?? "dev@localhost"));
        foreach (var recipient in email.Recipients) message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = "", HtmlBody = email.Html }.ToMessageBody();
        if (configuration["Email:Backend"] == "console")
        {
            logger.LogInformation("Email: {Message}", message.ToString());
            return;
        }
        using var client = new SmtpClient();
        var tls = configuration.GetValue<bool>("Email:UseTls") ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(configuration["Email:Host"] ?? "localhost", configuration.GetValue("Email:Port", 25), tls, cancellationToken);
        if (!string.IsNullOrEmpty(configuration["Email:Username"]))
            await client.AuthenticateAsync(configuration["Email:Username"]!, configuration["Email:Password"] ?? "", cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
