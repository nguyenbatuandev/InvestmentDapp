
namespace InvestDapp.Infrastructure.Data.Config
{
    public class BlockchainConfig
    {
        public string RpcUrl { get; set; } = string.Empty;
        public string ContractAddress { get; set; } = string.Empty;
        public int PollingIntervalSeconds { get; set; } = 10;
        public int BlockConfirmations { get; set; } = 5;
        public int MaxBlockRange { get; set; } = 2000; // <-- THÊM DÒNG NÀY
        public long ContractDeploymentBlock { get; set; } = 0; // <-- THÊM DÒNG NÀY

    }
}
