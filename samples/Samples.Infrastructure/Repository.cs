using Samples.Domain;

namespace Samples.Infrastructure;

public class Repository
{
    private readonly List<Entity> _store = [];

    public void Save(Entity entity) => _store.Add(entity);
}
