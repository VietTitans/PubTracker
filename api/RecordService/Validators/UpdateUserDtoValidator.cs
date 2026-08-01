using RecordService.Dtos;
using RecordService.Validation;

namespace RecordService.Validators;

public class UpdateUserDtoValidator : IValidator<UpdateUserDto>
{
    public ValidationResult Validate(UpdateUserDto obj)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(obj.Name))
        {
            result.AddError("Name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(obj.Username))
        {
            result.AddError("Username cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(obj.Email))
        {
            result.AddError("Email cannot be empty");
        }

        if (!obj.Email.Contains("@"))
        {
            result.AddError("Email must be a valid email address");
        }

        return result;
    }
}
