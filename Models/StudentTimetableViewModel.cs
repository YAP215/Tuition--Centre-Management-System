namespace TutionCenter.Models;

public class StudentTimetableViewModel
{
    public string Day { get; set; } = string.Empty;

    public string SubjectCode { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    public string ClassCode { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Room { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;
}