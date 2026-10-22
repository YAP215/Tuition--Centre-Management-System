namespace TutionCenter.Models;

public class SubjectListViewModel
{
    public IReadOnlyList<Subject> Subjects { get; init; } = Array.Empty<Subject>();
    public string Search { get; init; } = string.Empty;
    public string Status { get; init; } = "all";
    public string Sort { get; init; } = "code";
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
}
