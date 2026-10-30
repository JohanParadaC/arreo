namespace Arreo.Rope
{
    /// <summary>
    /// Where a rope view gets what to draw: the host's simulation, or on clients the state the
    /// host sent (ends, tension and state only; the curve is drawn locally).
    /// </summary>
    public interface IRopeSnapshotSource
    {
        bool IsConfigured { get; }
        RopeSnapshot Snapshot { get; }
    }
}
