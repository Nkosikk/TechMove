using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TechMove.Models;

public class ContractFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required, Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date), Display(Name = "End date")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

    public ContractStatus Status { get; set; }

    [Required, StringLength(100), Display(Name = "Service level")]
    public string ServiceLevel { get; set; } = string.Empty;

    [Display(Name = "Signed agreement (PDF)")]
    public IFormFile? AgreementFile { get; set; }

    public bool HasAgreement { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "The end date must be on or after the start date.",
                [nameof(EndDate)]);
        }

        if (Id == 0 && AgreementFile is null)
        {
            yield return new ValidationResult(
                "A signed agreement PDF is required.",
                [nameof(AgreementFile)]);
        }
    }
}
