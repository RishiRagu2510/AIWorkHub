namespace Persona.Domain.Careers;

public sealed class CareerLevel
{
    public long Id { get; set; }
    public long CareerProfileId { get; set; }
    public long GradeId { get; set; }
    public string CareerTitle { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CareerProfile CareerProfile { get; set; } = null!;
    public Grade Grade { get; set; } = null!;
}