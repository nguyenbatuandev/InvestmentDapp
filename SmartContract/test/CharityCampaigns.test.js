// test/InvestCampaigns.test.js

const { loadFixture, time } = require("@nomicfoundation/hardhat-toolbox/network-helpers");
const { expect } = require("chai");
const { ethers } = require("hardhat");

// ---- BỘ TEST CHI TIẾT CHO HỢP ĐỒNG InvestCampaigns ----
describe("InvestCampaigns Exhaustive Tests", function () {

  // Fixture cơ bản: Triển khai hợp đồng và thiết lập vai trò ban đầu
  async function deployAndSetupRolesFixture() {
    const [owner, admin, creator, investor1, investor2, nonRoleUser, feeReceiver] = await ethers.getSigners();
    const InvestCampaignsFactory = await ethers.getContractFactory("InvestCampaigns");
    const investCampaigns = await InvestCampaignsFactory.deploy();

    const DEFAULT_ADMIN_ROLE = await investCampaigns.DEFAULT_ADMIN_ROLE();
    const ADMIN_ROLE = await investCampaigns.ADMIN_ROLE();
    const CREATOR_ROLE = await investCampaigns.CREATOR_ROLE();

    // Owner (deployer) cấp quyền Admin cho tài khoản `admin`
    await investCampaigns.connect(owner).grantAdmin(admin.address);

    // Thiết lập người nhận phí và tỷ lệ phí (chỉ Admin)
    await investCampaigns.connect(owner).setAddressReceiver(feeReceiver.address);
    await investCampaigns.connect(owner).setFees(10); // 10% fee

    return {
      investCampaigns, owner, admin, creator, investor1, investor2, nonRoleUser, feeReceiver,
      DEFAULT_ADMIN_ROLE, ADMIN_ROLE, CREATOR_ROLE,
    };
  }

  // Fixture nâng cao: Tạo sẵn một chiến dịch đang hoạt động
  async function activeCampaignFixture() {
    const context = await loadFixture(deployAndSetupRolesFixture);
    const goal = ethers.parseEther("10");
    const durationInDays = 30;
    // Admin tạo chiến dịch cho địa chỉ của `creator`
    await context.investCampaigns
      .connect(context.admin)
      .createCampaign("Active Campaign", goal, durationInDays, context.creator.address);
    return { ...context, campaignId: 1, campaignGoal: goal, durationInDays };
  }

  // =================================================================
  // == CÁC BÀI TEST
  // =================================================================

  // ---- TEST 1: TRIỂN KHAI VÀ QUẢN LÝ VAI TRÒ ----
  context("Deployment and Role Management", function () {
    it("Should grant initial roles correctly to the deployer", async function () {
      const { investCampaigns, owner, DEFAULT_ADMIN_ROLE, ADMIN_ROLE, CREATOR_ROLE } = await loadFixture(deployAndSetupRolesFixture);
      expect(await investCampaigns.hasRole(DEFAULT_ADMIN_ROLE, owner.address)).to.be.true;
      expect(await investCampaigns.hasRole(ADMIN_ROLE, owner.address)).to.be.true;
      expect(await investCampaigns.hasRole(CREATOR_ROLE, owner.address)).to.be.true; // hợp đồng cấp luôn trong constructor
    });

    it("Should NOT allow non-admins to call admin-only functions", async function () {
      const { investCampaigns, creator, feeReceiver } = await loadFixture(deployAndSetupRolesFixture);
      await expect(investCampaigns.connect(creator).setFees(5))
        .to.be.revertedWith("Caller is not an admin");
      await expect(investCampaigns.connect(creator).setAddressReceiver(feeReceiver.address))
        .to.be.revertedWith("Caller is not an admin");
    });

    it("Should allow default admin to grant ADMIN_ROLE", async function () {
      const { investCampaigns, owner, nonRoleUser, ADMIN_ROLE } = await loadFixture(deployAndSetupRolesFixture);
      await expect(investCampaigns.connect(owner).grantAdmin(nonRoleUser.address))
        .to.emit(investCampaigns, "RoleGranted")
        .withArgs(ADMIN_ROLE, nonRoleUser.address, owner.address);
      expect(await investCampaigns.hasRole(ADMIN_ROLE, nonRoleUser.address)).to.be.true;
    });
  });

  // ---- TEST 3: TẠO CHIẾN DỊCH ----
  context("Campaign Creation", function () {
    it("Should fail if goal amount is 0", async function () {
      const { investCampaigns, admin, creator } = await loadFixture(deployAndSetupRolesFixture);
      await expect(investCampaigns.connect(admin).createCampaign("Test", 0, 10, creator.address))
        .to.be.revertedWith("Goal must be greater than 0");
    });

    it("Should fail if duration is 0", async function () {
      const { investCampaigns, admin, creator } = await loadFixture(deployAndSetupRolesFixture);
      await expect(investCampaigns.connect(admin).createCampaign("Test", 100, 0, creator.address))
        .to.be.revertedWith("Duration must be greater than 0");
    });

    it("Should create a campaign successfully with valid parameters", async function () {
      const { investCampaigns, admin, creator } = await loadFixture(deployAndSetupRolesFixture);
      const goal = ethers.parseEther("5");
      const durationInDays = 10;
      const tx = await investCampaigns.connect(admin).createCampaign("New Campaign", goal, durationInDays, creator.address);
      const receipt = await tx.wait();
      const block = await ethers.provider.getBlock(receipt.blockNumber);
      const expectedEndTime = block.timestamp + (durationInDays * 24 * 60 * 60);

      await expect(tx)
        .to.emit(investCampaigns, "CampaignCreated")
        .withArgs(1, creator.address, "New Campaign", goal, expectedEndTime);
    });
  });

  // ---- TEST 4: LUỒNG HOẠT ĐỘNG HOÀN CHỈNH ----
  context("Full Workflow Scenarios", function () {

    // --- Kịch bản 1: Thất bại và Hoàn tiền ---
    context("When a campaign fails (Refund)", function () {
      let failedCampaign;
      before(async function () {
        const context = await loadFixture(activeCampaignFixture);
        const investmentAmount = ethers.parseEther("1");
        await context.investCampaigns.connect(context.investor1).invest(context.campaignId, { value: investmentAmount });

        await time.increase(context.durationInDays * 24 * 60 * 60 + 1);
        await context.investCampaigns.connect(context.owner).updateCampaignStatus(context.campaignId);

        failedCampaign = { ...context, investmentAmount };
      });

      it("Should update status to Failed", async function () {
        const { investCampaigns, campaignId } = failedCampaign;
        const campaign = await investCampaigns.campaigns(campaignId);
        expect(campaign.status).to.equal(3); // Failed
      });

      it("Should allow investors to get a full refund", async function () {
        const { investCampaigns, investor1, campaignId, investmentAmount } = failedCampaign;
        await expect(investCampaigns.connect(investor1).refund(campaignId))
          .to.changeEtherBalances([investCampaigns, investor1], [-investmentAmount, investmentAmount]);
      });

      it("Should prevent double refunds", async function () {
        const local = await loadFixture(activeCampaignFixture);
        const localInvestment = ethers.parseEther("1");
        await local.investCampaigns.connect(local.investor1).invest(local.campaignId, { value: localInvestment });
        await time.increase(local.durationInDays * 24 * 60 * 60 + 1);
        await local.investCampaigns.updateCampaignStatus(local.campaignId);
        await local.investCampaigns.connect(local.investor1).refund(local.campaignId);
        await expect(local.investCampaigns.connect(local.investor1).refund(local.campaignId))
          .to.be.revertedWith("No investment to refund");
      });
    });

    // --- Kịch bản 2: Thành công và Rút vốn ban đầu ---
    context("When a campaign is successful (Capital Withdrawal)", function () {
      let successfulCampaign;
      before(async function () {
        const context = await loadFixture(activeCampaignFixture);
        await context.investCampaigns.connect(context.investor1).invest(context.campaignId, { value: ethers.parseEther("6") });
        await context.investCampaigns.connect(context.investor2).invest(context.campaignId, { value: ethers.parseEther("4") });

        await time.increase(context.durationInDays * 24 * 60 * 60 + 1);
        await context.investCampaigns.connect(context.owner).updateCampaignStatus(context.campaignId);

        successfulCampaign = context;
      });

      it("Should update status to Voting and store totalRaisedAmount", async function () {
        const { investCampaigns, campaignId } = successfulCampaign;
        const campaign = await investCampaigns.campaigns(campaignId);
        expect(campaign.status).to.equal(1); // Voting
        expect(campaign.totalRaisedAmount).to.equal(ethers.parseEther("10"));
      });

      it("Should allow owner to request withdrawal and investors to vote", async function () {
        const { investCampaigns, creator, investor1, campaignId } = successfulCampaign;
        await expect(investCampaigns.connect(creator).requestFullWithdrawal(campaignId, "Withdraw capital"))
          .to.emit(investCampaigns, "WithdrawalRequested");

        await investCampaigns.connect(investor1).voteForWithdrawal(campaignId, 0, true);
        const request = await investCampaigns.withdrawalRequests(campaignId, 0);
        expect(request.agreeVotes).to.equal(ethers.parseEther("6"));
      });

      it("Should allow withdrawal after successful vote and transfer fees", async function () {
        const { investCampaigns, creator, investor2, feeReceiver, campaignId } = successfulCampaign;

        await investCampaigns.connect(investor2).voteForWithdrawal(campaignId, 0, true);
        await time.increase(3 * 24 * 60 * 60 + 1); // VOTE_DURATION

        const totalRaised = (await investCampaigns.campaigns(campaignId)).totalRaisedAmount;
        const feeAmount = (totalRaised * 10n) / 100n; // 10%
        const amountToCreator = totalRaised - feeAmount;

        await expect(investCampaigns.connect(creator).checkAndExecuteWithdrawal(campaignId, 0))
          .to.changeEtherBalances(
            [investCampaigns, creator, feeReceiver],
            [-totalRaised, amountToCreator, feeAmount]
          );
      });
    });

    // --- Kịch bản 3: Thành công, Thêm và Chia Lợi nhuận (SAU KHI ĐÃ RÚT VỐN) ---
    context("When a successful campaign shares profit", function () {
      let contextData;
      const investment1_amount = ethers.parseEther("6"); // 60%
      const investment2_amount = ethers.parseEther("4"); // 40%
      const totalInvestment = ethers.parseEther("10");

      before(async function () {
        const base = await loadFixture(activeCampaignFixture);

        await base.investCampaigns.connect(base.investor1).invest(base.campaignId, { value: investment1_amount });
        await base.investCampaigns.connect(base.investor2).invest(base.campaignId, { value: investment2_amount });

        await time.increase(base.durationInDays * 24 * 60 * 60 + 1);
        await base.investCampaigns.connect(base.owner).updateCampaignStatus(base.campaignId);

        await base.investCampaigns.connect(base.creator).requestFullWithdrawal(base.campaignId, "Initial capital withdrawal");
        await base.investCampaigns.connect(base.investor1).voteForWithdrawal(base.campaignId, 0, true);
        await base.investCampaigns.connect(base.investor2).voteForWithdrawal(base.campaignId, 0, true);

        await time.increase(3 * 24 * 60 * 60 + 1);
        await base.investCampaigns.connect(base.creator).checkAndExecuteWithdrawal(base.campaignId, 0);

        contextData = { ...base };
      });

      it("Should allow creator to add profit to the campaign", async function () {
        const { investCampaigns, creator, campaignId } = contextData;
        const profitAmount = ethers.parseEther("2");

        await expect(investCampaigns.connect(creator).addProfit(campaignId, { value: profitAmount }))
          .to.emit(investCampaigns, "ProfitAdded")
          .withArgs(0, campaignId, profitAmount);

        expect(await ethers.provider.getBalance(investCampaigns.target)).to.equal(profitAmount);
      });

      it("Should allow investor 1 to claim their 60% share of the profit", async function () {
        const { investCampaigns, investor1, campaignId } = contextData;
        const profitAmount = ethers.parseEther("2");
        const profitIndex = 0;
        const expectedShare = (profitAmount * investment1_amount) / totalInvestment;

        await expect(investCampaigns.connect(investor1).claimProfit(campaignId, profitIndex))
          .to.changeEtherBalances([investCampaigns, investor1], [-expectedShare, expectedShare]);
      });

      it("Should allow investor 2 to claim their 40% share of the profit", async function () {
        const { investCampaigns, investor2, campaignId } = contextData;
        const profitAmount = ethers.parseEther("2");
        const profitIndex = 0;
        const expectedShare = (profitAmount * investment2_amount) / totalInvestment;

        await expect(investCampaigns.connect(investor2).claimProfit(campaignId, profitIndex))
          .to.changeEtherBalances([investCampaigns, investor2], [-expectedShare, expectedShare]);
      });

      it("Should REVERT if an investor tries to claim the same profit again", async function () {
        const { investCampaigns, investor1, campaignId } = contextData;
        const profitIndex = 0;

        await expect(investCampaigns.connect(investor1).claimProfit(campaignId, profitIndex))
          .to.be.revertedWith("Profit already claimed");
      });

      it("Should correctly handle a second round of profit distribution", async function () {
        const { investCampaigns, creator, investor1, investor2, campaignId } = contextData;
        const secondProfitAmount = ethers.parseEther("1");

        await expect(investCampaigns.connect(creator).addProfit(campaignId, { value: secondProfitAmount }))
          .to.emit(investCampaigns, "ProfitAdded")
          .withArgs(1, campaignId, secondProfitAmount);

        const profitIndex = 1;

        const expectedShare1 = (secondProfitAmount * investment1_amount) / totalInvestment;
        await expect(investCampaigns.connect(investor1).claimProfit(campaignId, profitIndex))
          .to.changeEtherBalances([investCampaigns, investor1], [-expectedShare1, expectedShare1]);

        const expectedShare2 = (secondProfitAmount * investment2_amount) / totalInvestment;
        await expect(investCampaigns.connect(investor2).claimProfit(campaignId, profitIndex))
          .to.changeEtherBalances([investCampaigns, investor2], [-expectedShare2, expectedShare2]);

        await expect(investCampaigns.connect(investor1).claimProfit(campaignId, profitIndex))
          .to.be.revertedWith("Profit already claimed");
      });

      it("Should REVERT if a non-investor tries to claim profit", async function () {
        const { investCampaigns, nonRoleUser, campaignId } = contextData;
        const profitIndex = 0;

        await expect(investCampaigns.connect(nonRoleUser).claimProfit(campaignId, profitIndex))
          .to.be.revertedWith("No investments found for this user");
      });
    });
  });
});
