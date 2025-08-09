import { ethers, run } from "hardhat";

async function main() {
  const [deployer] = await ethers.getSigners();
  console.log("Deploying with address:", deployer.address);

  const ContractFactory = await ethers.getContractFactory("InvestCampaignsView");
  const contract = await ContractFactory.deploy();
  await contract.waitForDeployment();

  const address = await contract.getAddress();
  console.log("Deployed to:", address);

  // Đợi BscScan index (30s thường là đủ)
  console.log("Verifying on BscScan...");
  await new Promise((res) => setTimeout(res, 30000));

  await run("verify:verify", {
    address,
    constructorArguments: [] // ✅ KHÔNG có tham số constructor
  });

  console.log("✅ Verified successfully!");
}

main().catch((err) => {
  console.error("❌ Deployment or verification failed:", err);
  process.exit(1);
});
