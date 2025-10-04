
namespace InvestDapp.Infrastructure.Data.Config
{
    public class BlockchainConfig
    {
        public string RpcUrl { get; set; } = string.Empty;
        public string ContractAddress { get; set; } = string.Empty;
        public string RoleManagerContractAddress { get; set; } = string.Empty;
        public string DefaultAdminPrivateKey { get; set; } = string.Empty;
        public string DefaultAdminAddress { get; set; } = string.Empty;
        public long ChainId { get; set; } = 97; // BSC Testnet default
        public int PollingIntervalSeconds { get; set; } = 10;
        public int BlockConfirmations { get; set; } = 5;
        public int MaxBlockRange { get; set; } = 2000;
        public long ContractDeploymentBlock { get; set; } = 0;
    }
}
