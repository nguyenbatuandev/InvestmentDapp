// SPDX-License-Identifier: MIT
pragma solidity 0.8.30;

import "@openzeppelin/contracts/access/AccessControl.sol";
import "@openzeppelin/contracts/security/ReentrancyGuard.sol";

contract InvestCampaigns is AccessControl, ReentrancyGuard {
    // --- ROLES ---
    bytes32 public constant ADMIN_ROLE   = keccak256("ADMIN_ROLE");
    bytes32 public constant CREATOR_ROLE = keccak256("CREATOR_ROLE");

    // --- ERRORS (rẻ gas & rõ nghĩa) ---
    error CampaignNotFound(uint256 id);
    error DirectETHNotAllowed();

    // --- ENUMS ---
    enum CampaignStatus { Active, Voting, Completed, Failed }
    enum WithdrawalStatus { Pending, Executed, Canceled }

    // --- STRUCTS ---
    struct Campaign {
        uint256 id;
        address owner;
        string  name;
        uint256 goalAmount;
        uint256 currentRaisedAmount;
        uint256 totalRaisedAmount;
        uint256 endTime;
        CampaignStatus status;
        uint256 investorCount;
        // (không thêm bool exists ở đây để tránh xáo trộn layout;
        // dùng mapping riêng campaignExists cho an toàn)
    }

    struct Investment {
        address investor;
        uint256 amount;
        uint256 timestamp;
    }

    struct WithdrawalRequest {
        uint256 id;
        address requester;
        uint256 amount;
        string  reason;
        WithdrawalStatus status;
        uint256 agreeVotes;
        uint256 disagreeVotes;
        uint256 voteEndTime;
    }

    struct Profit {
        uint256 id;
        uint256 campaignId;
        uint256 amount;
    }

    // --- STATE ---
    uint256 public addProfitCounter;
    uint256 public requestWithdrawCounter;
    uint256 public constant MIN_INVESTMENT_AMOUNT = 0.01 ether;
    uint256 public constant VOTE_DURATION = 3 days;
    uint16  public fees;
    address public feeReceiver;
    uint256 public campaignCounter;

    mapping(uint256 => Campaign) public campaigns;

    //   cờ tồn tại chiến dịch
    mapping(uint256 => bool) private campaignExists; // <<<<<<<<<<<<<<

    mapping(uint256 => mapping(address => uint256)) public campaignInvestorBalances;
    mapping(uint256 => Investment[]) public campaignInvestments;
    mapping(uint256 => WithdrawalRequest[]) public withdrawalRequests;
    mapping(uint256 => mapping(uint256 => mapping(address => bool))) public withdrawalVotes;
    mapping(uint256 => Profit[]) public listProfits;
    mapping(uint256 => uint256) public totalProfits;
    mapping(uint256 => mapping(uint256 => mapping(address => bool))) public profitClaimed;
    mapping(uint256 => uint16) public getDenialsRequestedWithDrawCampaigns;
    mapping(address => mapping(uint256 => uint256)) public withdrawWithFees;
    mapping(address => uint256) public totalWithdrawFees;
    mapping(uint256 => address[]) public campaignInvestors;

    // --- EVENTS ---
    event CampaignCreated(uint256 indexed id, address indexed owner, string name, uint256 goalAmount, uint256 endTime);
    event InvestmentReceived(uint256 indexed campaignId, address indexed investor, uint256 amount, uint256 currentRaisedAmount);
    event WithdrawalRequested(uint256 indexed campaignId, uint256 indexed requestId, address indexed requester, uint256 amount, string reason, uint256 voteEndTime);
    event VoteCast(uint256 indexed campaignId, uint256 indexed requestId, address indexed voter, bool agree, uint256 voteWeight);
    event WithdrawalExecuted(WithdrawalStatus status, uint256 indexed campaignId, uint256 indexed requestId, address indexed recipient, uint256 amount);
    event RefundIssued(uint256 indexed campaignId, address indexed investor, uint256 amount);
    event CampaignStatusUpdated(uint256 indexed campaignId, uint8 newStatus);
    event ProfitAdded(uint256 indexed id, uint256 indexed campaignId, uint256 amount);
    event ProfitClaimed(uint256 indexed campaignId, address indexed investor, uint256 amount);
    event CampaignCanceledByAdmin(uint256 indexed campaignId, address indexed admin);
    event WithdrawalRequestCanceled(uint256 indexed campaignId, uint256 indexed requestId);
    event SetFees(address indexed receiver, uint16 fees);
    event SetAddressReceiver(address indexed receiver, address indexed admin);
    event DepositBNBTrading(address indexed sender, uint256 amount);
    event WithdrawBNBTrading(address indexed receiver, uint256 amount);

    // --- MODIFIERS ---
    modifier onlyAdmin() {
        require(hasRole(ADMIN_ROLE, msg.sender), "Caller is not an admin");
        _;
    }

    // bắt buộc campaign tồn tại trước khi chạy logic
    modifier campaignMustExist(uint256 _campaignId) {
        if (!campaignExists[_campaignId]) revert CampaignNotFound(_campaignId);
        _;
    }

    modifier onlyCampaignOwner(uint256 _campaignId) {
        if (!campaignExists[_campaignId]) revert CampaignNotFound(_campaignId); //  thêm check tồn tại
        require(campaigns[_campaignId].owner == msg.sender, "Not campaign owner");
        _;
    }

    // --- CONSTRUCTOR ---
    constructor() {
        _grantRole(DEFAULT_ADMIN_ROLE, msg.sender);
        _grantRole(ADMIN_ROLE, msg.sender);
        _grantRole(CREATOR_ROLE, msg.sender);
        _setRoleAdmin(ADMIN_ROLE, DEFAULT_ADMIN_ROLE);
    }

    // --- ADMIN ROLES ---
    function grantAdmin(address _user) external {
        require(hasRole(DEFAULT_ADMIN_ROLE, msg.sender), "Caller is not the default admin");
        grantRole(ADMIN_ROLE, _user);
    }

    function revokeAdmin(address _user) external {
        require(hasRole(DEFAULT_ADMIN_ROLE, msg.sender), "Caller is not the default admin");
        revokeRole(ADMIN_ROLE, _user);
    }

    function setAddressReceiver(address _receiver) external onlyAdmin {
        require(_receiver != address(0), "Invalid fee receiver address");
        feeReceiver = _receiver;
        emit SetAddressReceiver(_receiver, msg.sender);
    }

    function setFees(uint16 _fees) external onlyAdmin {
        require(_fees <= 100, "Fees cannot exceed 100%");
        fees = _fees;
        emit SetFees(feeReceiver, _fees);
    }

    // --- ADMIN ACTIONS ---
    function cancelCampaignByAdmin(uint256 _campaignId) external onlyAdmin campaignMustExist(_campaignId) {
        Campaign storage campaign = campaigns[_campaignId];
        require(campaign.status == CampaignStatus.Active, "Only active campaigns can be canceled");
        campaign.status = CampaignStatus.Failed;
        emit CampaignCanceledByAdmin(_campaignId, msg.sender);
    }

    function createCampaign(
        uint256 _id,
        string memory _name,
        uint256 _goalAmount,
        uint256 _durationInDays,
        address _creatorAddress
    ) external onlyAdmin {
        require(_id != 0, "Invalid id");
        require(!campaignExists[_id], "ID already used");              //  dùng cờ tồn tại
        require(_creatorAddress != address(0), "Invalid creator");     //  thêm check
        require(_goalAmount > 0, "Goal must be greater than 0");
        require(_durationInDays > 0, "Duration must be greater than 0");

        campaignCounter++;
        uint256 endTime = block.timestamp + _durationInDays * 1 days;

        campaigns[_id] = Campaign({
            id: _id,
            owner: _creatorAddress,
            name: _name,
            goalAmount: _goalAmount,
            currentRaisedAmount: 0,
            totalRaisedAmount: 0,
            endTime: endTime,
            status: CampaignStatus.Active,
            investorCount: 0
        });

        campaignExists[_id] = true;                                     //  ĐÁNH DẤU TỒN TẠI

        emit CampaignCreated(_id, _creatorAddress, _name, _goalAmount, endTime);
    }

    // --- CORE FUNCTIONS ---
    function invest(uint256 _campaignId)
        external
        payable
        nonReentrant
        campaignMustExist(_campaignId)                                  //  chặn ID không tồn tại
    {
        require(_campaignId != 0, "Invalid campaign id");               //  thêm chặn id = 0
        Campaign storage c = campaigns[_campaignId];

        require(c.status == CampaignStatus.Active, "Campaign is not active");
        require(msg.value >= MIN_INVESTMENT_AMOUNT, "Investment too small");
        require(block.timestamp < c.endTime, "Campaign has ended");

        if (campaignInvestorBalances[_campaignId][msg.sender] == 0) {
            c.investorCount++;
            campaignInvestors[_campaignId].push(msg.sender);
        }

        campaignInvestorBalances[_campaignId][msg.sender] += msg.value;
        c.currentRaisedAmount += msg.value;

        campaignInvestments[_campaignId].push(Investment({
            investor: msg.sender,
            amount: msg.value,
            timestamp: block.timestamp
        }));

        emit InvestmentReceived(_campaignId, msg.sender, msg.value, c.currentRaisedAmount);
    }

    function addProfit(uint256 _campaignId)
        external
        payable
        onlyCampaignOwner(_campaignId)
    {
        require(campaigns[_campaignId].status == CampaignStatus.Completed, "Campaign not completed");
        require(msg.value > 0, "Profit must be greater than zero");

        uint256 newProfitId = addProfitCounter++;
        listProfits[_campaignId].push(Profit({
            id: newProfitId,
            campaignId: _campaignId,
            amount: msg.value
        }));

        totalProfits[_campaignId] += msg.value;
        emit ProfitAdded(newProfitId, _campaignId, msg.value);
    }

    function claimProfit(uint256 _campaignId, uint256 _profitIndex)
        external
        nonReentrant
        campaignMustExist(_campaignId)                                  // 
    {
        Campaign storage campaign = campaigns[_campaignId];
        require(campaign.status == CampaignStatus.Completed, "Campaign not completed");
        require(campaign.totalRaisedAmount > 0, "Invalid total investments for profit calculation");

        uint256 userInvestment = campaignInvestorBalances[_campaignId][msg.sender];
        require(userInvestment > 0, "No investments found for this user");

        require(_profitIndex < listProfits[_campaignId].length, "Profit index out of bounds");
        Profit storage profit = listProfits[_campaignId][_profitIndex];

        require(profit.amount > 0, "No profit to claim");
        require(!profitClaimed[_campaignId][_profitIndex][msg.sender], "Profit already claimed");

        uint256 profitShare = (profit.amount * userInvestment) / campaign.totalRaisedAmount;
        require(profitShare > 0, "Profit share is too small to claim");

        profitClaimed[_campaignId][_profitIndex][msg.sender] = true;

        (bool sent, ) = msg.sender.call{value: profitShare}("");
        require(sent, "Profit transfer failed");

        emit ProfitClaimed(_campaignId, msg.sender, profitShare);
    }

    function voteForWithdrawal(uint256 _campaignId, uint256 _requestId, bool _agree)
        external
        campaignMustExist(_campaignId)                                  // 
    {
        uint256 voteWeight = campaignInvestorBalances[_campaignId][msg.sender];
        require(voteWeight > 0, "Only investors can vote");
        require(_requestId < withdrawalRequests[_campaignId].length, "Withdrawal request not found");

        WithdrawalRequest storage request = withdrawalRequests[_campaignId][_requestId];
        require(block.timestamp <= request.voteEndTime, "Voting period has ended");
        require(request.status == WithdrawalStatus.Pending, "Request is not pending");
        require(!withdrawalVotes[_campaignId][_requestId][msg.sender], "You have already voted");

        withdrawalVotes[_campaignId][_requestId][msg.sender] = true;

        if (_agree) {
            request.agreeVotes += voteWeight;
        } else {
            request.disagreeVotes += voteWeight;
        }

        emit VoteCast(_campaignId, _requestId, msg.sender, _agree, voteWeight);
    }

    function checkAndExecuteWithdrawal(uint256 _campaignId, uint256 _requestId)
        external
        nonReentrant
        campaignMustExist(_campaignId)                                  // 
    {
        Campaign storage campaign = campaigns[_campaignId];
        require(_requestId < withdrawalRequests[_campaignId].length, "Withdrawal request not found");

        WithdrawalRequest storage request = withdrawalRequests[_campaignId][_requestId];
        require(request.status == WithdrawalStatus.Pending, "Request not pending");
        require(block.timestamp > request.voteEndTime, "Voting period not yet ended");

        if (request.agreeVotes > campaign.totalRaisedAmount / 2) {
            uint256 feeAmount = (request.amount * fees) / 100;
            uint256 amountToTransfer = request.amount - feeAmount;
            require(campaign.currentRaisedAmount >= request.amount, "Insufficient funds in campaign");

            request.status = WithdrawalStatus.Executed;
            campaign.currentRaisedAmount -= request.amount;
            campaign.status = CampaignStatus.Completed;

            if (fees > 0 && feeAmount > 0) {
                withdrawWithFees[feeReceiver][_campaignId] += feeAmount;
                totalWithdrawFees[feeReceiver] += feeAmount;
            }

            emit WithdrawalExecuted(WithdrawalStatus.Executed, _campaignId, _requestId, request.requester, amountToTransfer);
            emit CampaignStatusUpdated(_campaignId, uint8(CampaignStatus.Completed));

            (bool sent, ) = request.requester.call{value: amountToTransfer}("");
            require(sent, "Failed to send Ether to requester");

            if (fees > 0 && feeAmount > 0) {
                (bool feeSent, ) = feeReceiver.call{value: feeAmount}("");
                require(feeSent, "Failed to send fees");
            }
        } else {
            request.status = WithdrawalStatus.Canceled;
            getDenialsRequestedWithDrawCampaigns[_campaignId]++;
            emit WithdrawalExecuted(WithdrawalStatus.Canceled, _campaignId, _requestId, request.requester, 0);
        }
    }

    function refund(uint256 _campaignId)
        external
        nonReentrant
        campaignMustExist(_campaignId)
    {
        Campaign storage campaign = campaigns[_campaignId];
        require(campaign.status == CampaignStatus.Failed, "Campaign did not fail");

        uint256 refundAmount = campaignInvestorBalances[_campaignId][msg.sender];
        require(refundAmount > 0, "No investment to refund");

        campaignInvestorBalances[_campaignId][msg.sender] = 0;

        (bool sent, ) = msg.sender.call{value: refundAmount}("");
        require(sent, "Refund failed");

        emit RefundIssued(_campaignId, msg.sender, refundAmount);
    }

    function updateCampaignStatus(uint256 _campaignId)
        public
        campaignMustExist(_campaignId)                                  // 
    {
        Campaign storage campaign = campaigns[_campaignId];

        if (campaign.status == CampaignStatus.Active) {
            if (block.timestamp >= campaign.endTime) {
                if (campaign.currentRaisedAmount >= campaign.goalAmount) {
                    campaign.totalRaisedAmount = campaign.currentRaisedAmount;
                    campaign.status = CampaignStatus.Voting;
                    emit CampaignStatusUpdated(_campaignId, uint8(CampaignStatus.Voting));
                } else {
                    campaign.status = CampaignStatus.Failed;
                    emit CampaignStatusUpdated(_campaignId, uint8(CampaignStatus.Failed));
                }
            }
        } else if (campaign.status == CampaignStatus.Voting) {
            if (getDenialsRequestedWithDrawCampaigns[_campaignId] >= 3) {
                campaign.status = CampaignStatus.Failed;
                emit CampaignStatusUpdated(_campaignId, uint8(CampaignStatus.Failed));
            }
        }
    }

    function requestFullWithdrawal(uint256 _campaignId, string memory _reason)
        external
        onlyCampaignOwner(_campaignId)
    {
        updateCampaignStatus(_campaignId);
        Campaign storage campaign = campaigns[_campaignId];

        require(campaign.currentRaisedAmount > 0, "No funds to withdraw");
        require(getDenialsRequestedWithDrawCampaigns[_campaignId] < 3, "Too many withdrawal requests denied");
        require(campaign.status == CampaignStatus.Voting, "Campaign is not in voting status");
        require(campaign.currentRaisedAmount >= campaign.goalAmount, "Insufficient funds in campaign");

        uint256 requestCount = withdrawalRequests[_campaignId].length;
        if (requestCount > 0) {
            WithdrawalRequest storage lastRequest = withdrawalRequests[_campaignId][requestCount - 1];
            require(lastRequest.status == WithdrawalStatus.Canceled, "Previous withdrawal request is still active");
        }

        uint256 amountToWithdraw = campaign.currentRaisedAmount;
        uint256 id = requestWithdrawCounter++;
        uint256 voteEndTime = block.timestamp + VOTE_DURATION;

        withdrawalRequests[_campaignId].push(WithdrawalRequest({
            id: id,
            requester: msg.sender,
            amount: amountToWithdraw,
            reason: _reason,
            status: WithdrawalStatus.Pending,
            agreeVotes: 0,
            disagreeVotes: 0,
            voteEndTime: voteEndTime
        }));

        emit WithdrawalRequested(_campaignId, id, msg.sender, amountToWithdraw, _reason, voteEndTime);
    }

    function depositBNBTrading() external payable {
        require(msg.value > 0, "Amount must be greater than zero");
        emit DepositBNBTrading(msg.sender, msg.value);
    }

    function withdrawBNBTrading(uint256 _amount) external onlyAdmin nonReentrant {
        require(_amount > 0, "Amount must be greater than zero");
        require(address(this).balance >= _amount, "Insufficient contract balance");
        (bool sent, ) = msg.sender.call{value: _amount}("");
        require(sent, "Withdrawal failed");
        emit WithdrawBNBTrading(msg.sender, _amount);
    }

    //   chặn gửi ETH trực tiếp (tránh nhầm tưởng là invest)
    receive() external payable { revert DirectETHNotAllowed(); }
    fallback() external payable { revert DirectETHNotAllowed(); }
}
