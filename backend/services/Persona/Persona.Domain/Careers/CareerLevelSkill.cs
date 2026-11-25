namespace Persona.Domain.Careers;

public sealed class CareerLevelSkill
{
    public long Id { get; set; }
    public long CareerLevelId { get; set; }
    public long SkillId { get; set; }
    public int RequiredProficiency { get; set; }
    public int TargetProficiency { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CareerLevel CareerLevel { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}