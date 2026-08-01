using RecordService.Dtos;
using RecordService.Validation;

namespace RecordService.Validators;

public class SavedSearchCreateDtoValidator : IValidator<SavedSearchCreateDto>
{
    public ValidationResult Validate(SavedSearchCreateDto obj)
    {
        var result = new ValidationResult();

        if (obj.SearchQueryId <= 0)
        {
            result.AddError("SearchQueryId must be greater than 0");
        }

        return result;
    }
}
