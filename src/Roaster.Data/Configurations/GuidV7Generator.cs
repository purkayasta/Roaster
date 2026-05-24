using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Roaster.Data.Configurations;

public class GuidV7Generator : ValueGenerator<Guid>
{
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
    public override bool GeneratesTemporaryValues => false;
}
