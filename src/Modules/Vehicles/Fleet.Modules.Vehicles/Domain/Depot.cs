namespace Fleet.Modules.Vehicles.Domain;

/// <summary>Where vehicles are based. Four of them, and nobody opens a fifth during this course.</summary>
internal sealed class Depot
{
    // EF Core materialises through this constructor. It is private so that application code has
    // to go through the one below and cannot create a half-built entity.
    private Depot()
    {
        Name = string.Empty;
        City = string.Empty;
    }

    public Depot(Guid id, string name, string city)
    {
        Id = id;
        Name = name;
        City = city;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string City { get; private set; }
}
