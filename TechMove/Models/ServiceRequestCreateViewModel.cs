using System.ComponentModel.DataAnnotations;

namespace TechMove.Models;

public class ServiceRequestCreateViewModel
{
    [Required, Display(Name = "Contract")]
    public int ContractId { get; set; }

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999999.99), Display(Name = "Cost (USD)")]
    public decimal CostUsd { get; set; }
}
