namespace DistributedLock.Model;
#nullable enable
public class StateData
{
    public int Number { get; }
    public string? Analysis { get; set; }

    public StateData(int number, string? analysis = null)
    {
        Number = number;
        Analysis = analysis;
    }
}