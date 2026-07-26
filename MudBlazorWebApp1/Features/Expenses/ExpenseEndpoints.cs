using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Accounting;
using MudBlazorWebApp1.Features.Auth;
using MudBlazorWebApp1.Infrastructure;

namespace MudBlazorWebApp1.Features.Expenses;

public static class ExpenseEndpoints
{
    private const long MaximumFileSize = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/expenses").WithTags("Expenses").RequireAuthorization(Policies.TenantMember);
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.CanWrite);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Policies.CanWrite);
        group.MapPost("/{id:guid}/submit", SubmitAsync).RequireAuthorization(Policies.CanWrite);
        group.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(Policies.CanReviewAccounting);
        group.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(Policies.CanReviewAccounting);
        group.MapPost("/{id:guid}/attachments", UploadAttachmentAsync)
            .RequireAuthorization(Policies.CanWrite).DisableAntiforgery();
        group.MapGet("/{id:guid}/attachments/{attachmentId:guid}/download", GetAttachmentUrlAsync);
        group.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", DeleteAttachmentAsync)
            .RequireAuthorization(Policies.CanWrite);
        return endpoints;
    }

    private static async Task<ExpenseResponse[]> ListAsync(
        DateOnly? from, DateOnly? to, string? status, AppDbContext db, CancellationToken cancellationToken)
    {
        var query = db.Expenses.AsNoTracking().Include(x => x.Attachments).AsQueryable();
        if (from is not null) query = query.Where(x => x.CompetenceDate >= from);
        if (to is not null) query = query.Where(x => x.CompetenceDate <= to);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var expenses = await query.OrderByDescending(x => x.CompetenceDate).ThenByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);
        return expenses.Select(ToResponse).ToArray();
    }

    private static async Task<IResult> CreateAsync(
        ExpenseRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem) return problem;
        var expense = new Expense
        {
            Description = request.Description.Trim(), Category = request.Category,
            Supplier = Clean(request.Supplier), CompetenceDate = request.CompetenceDate,
            DueDate = request.DueDate, Amount = request.Amount, Notes = Clean(request.Notes),
            CreatedByUserId = UserId(user), UpdatedAt = timeProvider.GetUtcNow()
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/expenses/{expense.Id}", ToResponse(expense));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, ExpenseRequest request, AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem) return problem;
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status is ExpenseStatuses.Approved or ExpenseStatuses.PendingReview)
            return Results.Conflict(new { message = "Only draft or rejected expenses can be edited." });
        expense.Description = request.Description.Trim();
        expense.Category = request.Category;
        expense.Supplier = Clean(request.Supplier);
        expense.CompetenceDate = request.CompetenceDate;
        expense.DueDate = request.DueDate;
        expense.Amount = request.Amount;
        expense.Notes = Clean(request.Notes);
        expense.Status = ExpenseStatuses.Draft;
        expense.RejectionReason = null;
        expense.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(expense));
    }

    private static async Task<IResult> SubmitAsync(Guid id, AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status is not (ExpenseStatuses.Draft or ExpenseStatuses.Rejected))
            return Results.Conflict(new { message = "Expense is not available for submission." });
        if (expense.Attachments.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = ["A supporting document is required."] });
        expense.Status = ExpenseStatuses.PendingReview;
        expense.RejectionReason = null;
        expense.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(expense));
    }

    private static async Task<IResult> ApproveAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, AccountingEngine accounting,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status != ExpenseStatuses.PendingReview)
            return Results.Conflict(new { message = "Only expenses pending review can be approved." });
        if (expense.Attachments.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = ["A supporting document is required."] });
        expense.Status = ExpenseStatuses.Approved;
        expense.ReviewedByUserId = UserId(user);
        expense.ReviewedAt = timeProvider.GetUtcNow();
        expense.UpdatedAt = timeProvider.GetUtcNow();
        await accounting.ApplyExpenseApprovalAsync(expense, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(expense));
    }

    private static async Task<IResult> RejectAsync(
        Guid id, RejectExpenseRequest request, ClaimsPrincipal user, AppDbContext db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Reason is required."] });
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status != ExpenseStatuses.PendingReview)
            return Results.Conflict(new { message = "Only expenses pending review can be rejected." });
        expense.Status = ExpenseStatuses.Rejected;
        expense.RejectionReason = request.Reason.Trim();
        expense.ReviewedByUserId = UserId(user);
        expense.ReviewedAt = timeProvider.GetUtcNow();
        expense.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(expense));
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid id, IFormFile file, ClaimsPrincipal user, AppDbContext db,
        IExpenseStorage storage, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status is ExpenseStatuses.Approved or ExpenseStatuses.PendingReview)
            return Results.Conflict(new { message = "Attachments are locked while the expense is under review or approved." });
        if (file.Length is <= 0 or > MaximumFileSize)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["File must be between 1 byte and 10 MB."] });

        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var contentType = DetectContentType(bytes);
        if (contentType is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Only valid PDF, PNG and JPEG documents are accepted."] });
        var fileName = Path.GetFileName(file.FileName);
        var extension = contentType switch { "application/pdf" => ".pdf", "image/png" => ".png", _ => ".jpg" };
        var objectKey = $"tenants/{expense.TenantId:N}/expenses/{expense.Id:N}/{Guid.NewGuid():N}{extension}";
        memory.Position = 0;
        await storage.PutAsync(objectKey, memory, contentType, cancellationToken);
        var attachment = new ExpenseAttachment
        {
            TenantId = expense.TenantId, ExpenseId = expense.Id, ObjectKey = objectKey,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"comprovante{extension}" : fileName,
            ContentType = contentType, Size = file.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            UploadedByUserId = UserId(user)
        };
        db.ExpenseAttachments.Add(attachment);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch { await storage.DeleteAsync(objectKey, cancellationToken); throw; }
        return Results.Ok(ToResponse(attachment));
    }

    private static async Task<IResult> GetAttachmentUrlAsync(
        Guid id, Guid attachmentId, AppDbContext db, IExpenseStorage storage, CancellationToken cancellationToken)
    {
        var attachment = await db.ExpenseAttachments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId && x.ExpenseId == id, cancellationToken);
        return attachment is null ? Results.NotFound() : Results.Ok(new AttachmentDownloadResponse(
            storage.CreateDownloadUrl(attachment.ObjectKey, attachment.FileName)));
    }

    private static async Task<IResult> DeleteAttachmentAsync(
        Guid id, Guid attachmentId, AppDbContext db, IExpenseStorage storage, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (expense is null) return Results.NotFound();
        if (expense.Status is ExpenseStatuses.Approved or ExpenseStatuses.PendingReview)
            return Results.Conflict(new { message = "Attachments are locked." });
        var attachment = expense.Attachments.SingleOrDefault(x => x.Id == attachmentId);
        if (attachment is null) return Results.NotFound();
        await storage.DeleteAsync(attachment.ObjectKey, cancellationToken);
        db.ExpenseAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult? Validate(ExpenseRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Description)) errors["description"] = ["Description is required."];
        if (!ExpenseCategories.All.Contains(request.Category)) errors["category"] = ["Invalid expense category."];
        if (request.Amount <= 0) errors["amount"] = ["Amount must be positive."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        return null;
    }

    private static ExpenseResponse ToResponse(Expense expense) => new(
        expense.Id, expense.Description, expense.Category, expense.Supplier, expense.CompetenceDate,
        expense.DueDate, expense.Amount, expense.Currency, expense.Status, expense.Notes,
        expense.RejectionReason, expense.CreatedAt, expense.UpdatedAt,
        expense.Attachments.Select(ToResponse).ToArray());
    private static ExpenseAttachmentResponse ToResponse(ExpenseAttachment attachment) => new(
        attachment.Id, attachment.FileName, attachment.ContentType, attachment.Size,
        attachment.Sha256, attachment.CreatedAt);
}

public sealed record ExpenseRequest(
    [property: Required, MaxLength(300)] string Description,
    [property: Required] string Category,
    [property: MaxLength(200)] string? Supplier,
    DateOnly CompetenceDate,
    DateOnly? DueDate,
    decimal Amount,
    [property: MaxLength(1000)] string? Notes);
public sealed record RejectExpenseRequest([property: Required, MaxLength(600)] string Reason);
public sealed record ExpenseResponse(
    Guid Id, string Description, string Category, string? Supplier, DateOnly CompetenceDate,
    DateOnly? DueDate, decimal Amount, string Currency, string Status, string? Notes,
    string? RejectionReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    ExpenseAttachmentResponse[] Attachments);
public sealed record ExpenseAttachmentResponse(
    Guid Id, string FileName, string ContentType, long Size, string Sha256, DateTimeOffset CreatedAt);
public sealed record AttachmentDownloadResponse(string Url);
