import { HardhatUserConfig } from "hardhat/config";
import "@nomicfoundation/hardhat-toolbox";
import "@nomicfoundation/hardhat-verify";
import * as dotenv from "dotenv";

dotenv.config();

const config: HardhatUserConfig = {
  solidity: {
    version: "0.8.30",
    settings: {
      optimizer: {
        enabled: true, 
        runs: 200
      }
    }
  },
  networks: {
    bscTestnet: {
      url: "https://data-seed-prebsc-1-s1.binance.org:8545/",
      chainId: 97,
      accounts: [process.env.PRIVATE_KEY!]
    }
  },
  etherscan: {
  apiKey: process.env.BSCSCAN_API_KEY,
  customChains: [
    {
      network: "bscTestnet",
      chainId: 97,
      urls: {
        apiURL: "https://api-testnet.bscscan.com/api",
        browserURL: "https://testnet.bscscan.com"
      }
    }
  ]
}
};

export default config;
