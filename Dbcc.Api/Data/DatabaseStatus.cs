namespace Dbcc.Api.Data;

public sealed class DatabaseStatus
{
    private int _ready;
    public bool IsReady => Volatile.Read(ref _ready) == 1;
    public void SetReady(bool ready) => Volatile.Write(ref _ready, ready ? 1 : 0);
}
