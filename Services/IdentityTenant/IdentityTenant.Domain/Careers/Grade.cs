namespace IdentityTenant.Domain.Careers;

public sealed class Grade
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string? Family { get; set; }
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}