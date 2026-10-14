/// <summary>An explosive that can be removed atomically without firing its normal blast/fuse.
/// Containers consume only their payload. A false result never consumes an item.</summary>
public interface IConsumableExplosive
{
    bool TryConsumeExplosive(out int attackPower);
}
