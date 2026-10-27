namespace Core;

// What checking a step (or a whole plan) found:
//   Problems = it can't work, so nothing gets generated
//   Notes    = it will run, but it's worth a second look (e.g. it changes nothing)
public class CheckResult
{
    public List<string> Problems { get; } = new();
    public List<string> Notes { get; } = new();

    public bool HasProblems => Problems.Count > 0;
    public bool HasNotes => Notes.Count > 0;

    public void Problem(string message) => Problems.Add(message);
    public void Note(string message) => Notes.Add(message);

    public void Add(CheckResult other)
    {
        Problems.AddRange(other.Problems);
        Notes.AddRange(other.Notes);
    }
}
