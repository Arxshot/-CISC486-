// Each local player owns one magazine per slot. A reload only refills it on completion.
public sealed class LocalMagazine
{
    public int Capacity { get; }
    public int Rounds { get; private set; }
    public bool IsReloading => reloadEndsAt.HasValue;

    private readonly double reloadDuration;
    private double? reloadEndsAt;

    public LocalMagazine(int capacity, double duration)
    {
        Capacity = System.Math.Max(1, capacity);
        Rounds = Capacity;
        reloadDuration = System.Math.Max(0, duration);
    }

    public bool TryConsume()
    {
        if (IsReloading || Rounds == 0)
            return false;
        Rounds--;
        return true;
    }

    public bool StartReload(double now)
    {
        if (IsReloading || Rounds == Capacity)
            return false;
        reloadEndsAt = now + reloadDuration;
        return true;
    }

    public void UpdateReload(double now)
    {
        if (reloadEndsAt.HasValue && now >= reloadEndsAt.Value)
        {
            Rounds = Capacity;
            reloadEndsAt = null;
        }
    }

    public void CancelReload() => reloadEndsAt = null;
}
