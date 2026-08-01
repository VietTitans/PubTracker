namespace RecordService.Validation;

public interface IValidator<T>
{
    ValidationResult Validate(T obj);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public ValidationResult(bool isValid = true)
    {
        IsValid = isValid;
    }

    public ValidationResult AddError(string error)
    {
        IsValid = false;
        Errors.Add(error);
        return this;
    }
}
