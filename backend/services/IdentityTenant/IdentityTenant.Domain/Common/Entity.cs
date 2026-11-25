namespace IdentityTenant.Domain.Common;

public abstract class Entity
{
    protected Entity()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    protected Entity(long id)
    {
        Id = id;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public long Id { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    protected void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}