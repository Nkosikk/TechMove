using TechMove.Api.Models;
using TechMove.Contracts;

namespace TechMove.Api.Services;

public class ContractWorkflowService
{
    public bool CanCreateServiceRequest(Contract contract) =>
        contract.Status == ContractStatus.Active;

    public void EnsureServiceRequestAllowed(Contract contract)
    {
        if (!CanCreateServiceRequest(contract))
        {
            throw new InvalidOperationException(
                $"Service requests cannot be created for contracts with status '{contract.Status}'.");
        }
    }
}

