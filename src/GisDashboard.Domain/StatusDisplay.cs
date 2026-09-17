namespace GisDashboard.Domain;

public static class StatusDisplay
{
    public static string Label(string name) => name switch
    {
        "In Progress" => "Active",
        "Held" => "On-Hold",
        "Worked" => "Complete",
        _ => name
    };
}
