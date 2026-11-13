namespace TaskManager.Web;

public sealed record BoardFilter(string Search = "", string Executor = BoardFilter.AllExecutors, bool OverdueOnly = false, string? Status = null)
{
    public const string AllExecutors = "";
    public const string Unassigned = "unassigned";
}

public sealed record ExecutorOption(string Value, string Label);
