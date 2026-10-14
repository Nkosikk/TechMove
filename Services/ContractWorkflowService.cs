using TechMove.Models;

namespace TechMove.Services;

public class ContractWorkflowService
{
    public bool CanCreateServiceRequest(Contract contract) =>
        contract.Status is not ContractStatus.Expired and not ContractStatus.OnHold;

    public void EnsureServiceRequestAllowed(Contract contract)
    {
        if (!CanCreateServiceRequest(contract))
        {
            throw new InvalidOperationException(
                $"Service requests cannot be created for contracts with status '{contract.Status}'.");
        }
    }
}
