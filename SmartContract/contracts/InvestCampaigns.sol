// SPDX-License-Identifier: MIT
pragma solidity 0.8.30;

import "@openzeppelin/contracts/access/AccessControl.sol";
import "@openzeppelin/contracts/security/ReentrancyGuard.sol";

/**
 * @title InvestmentCampaigns
 * @dev Hợp đồng thông minh để gọi vốn đầu tư cộng đồng. Các chiến dịch có thể chia lợi nhuận cho nhà đầu tư theo tỷ lệ góp vốn.
 */
contract InvestCampaigns is AccessControl, ReentrancyGuard {
    // --- KHAI BÁO CÁC VAI TRÒ (ROLES) CHO AccessControl ---
    // Mỗi vai trò được định danh bằng một giá trị bytes32 duy nhất, tạo ra bằng cách băm (hash) tên của vai trò.
    bytes32 public constant ADMIN_ROLE = keccak256("ADMIN_ROLE");
    bytes32 public constant CREATOR_ROLE = keccak256("CREATOR_ROLE");
    // bytes32 public constant ALLOWED_ROLE = keccak256("ALLOWED_ROLE");

    // --- ENUMS (KIỂU LIỆT KÊ) ---
    // Dùng để định nghĩa các trạng thái có thể có, giúp mã nguồn dễ đọc và tránh các lỗi nhập liệu sai.
    enum CampaignStatus { Active, Voting, Completed, Failed } // 0, 1, 2, 3
    enum WithdrawalStatus { Pending, Executed, Canceled } // Trạng thái cho các yêu cầu rút tiền.

    // --- STRUCTS (CẤU TRÚC DỮ LIỆU) ---
    // Struct dùng để nhóm các biến liên quan lại với nhau, tạo thành một kiểu dữ liệu mới.
    struct Campaign {
        uint256 id;                 // ID duy nhất của chiến dịch.
        address owner;              // Địa chỉ ví của người tạo ra chiến dịch này.
        string name;                // Tên của chiến dịch.
        uint256 goalAmount;         // Số tiền mục tiêu cần kêu gọi (đơn vị: wei).
        uint256 currentRaisedAmount;// Số tiền hiện tại đã kêu gọi được (đơn vị: wei).
        uint256 totalRaisedAmount;  // Tổng số tiền đã huy động, dùng làm cơ sở chia lợi nhuận.
        uint256 endTime;            // Thời điểm chiến dịch kết thúc (dạng Unix timestamp).
        CampaignStatus status;      // Trạng thái hiện tại của chiến dịch (Đang hoạt động, Hoàn thành, Thất bại).
        uint256 investorCount;      // Số lượng các nhà tài trợ đã tham gia.
    }

    // Struct lưu trữ thông tin về một lần quyên góp cụ thể.
    struct Investment {
        address investor;           // Địa chỉ ví của người quyên góp.
        uint256 amount;             // Số tiền họ đã quyên góp trong lần đó.
        uint256 timestamp;          // Thời điểm thực hiện quyên góp.
    }

    // Struct lưu trữ thông tin của một yêu cầu rút tiền do chủ chiến dịch tạo ra.
    struct WithdrawalRequest {
        uint256 id;                 // ID của yêu cầu trong một chiến dịch.
        address requester;          // Địa chỉ của người yêu cầu rút (phải là chủ chiến dịch).
        uint256 amount;             // Số tiền được yêu cầu rút.
        string reason;              // Lý do cho việc rút tiền.
        WithdrawalStatus status;    // Trạng thái của yêu cầu (Đang chờ, Đã thực thi, Đã hủy).
        uint256 agreeVotes;         // Tổng "trọng số" phiếu đồng ý (tính bằng tổng số tiền góp).
        uint256 disagreeVotes;      // Tổng "trọng số" phiếu không đồng ý.
        uint256 voteEndTime;        // Thời điểm kết thúc phiên bỏ phiếu cho yêu cầu này.
    }

    struct Profit {
        uint256 id;                 // ID duy nhất của lợi nhuận (có thể là ID chiến dịch).
        uint256 campaignId;         // ID của chiến dịch mà lợi nhuận này thuộc về.
        uint256 amount;             // Số tiền lợi nhuận được thêm vào.
    }

    // --- BIẾN TRẠNG THÁI (STATE VARIABLES) ---
    // Các biến này được lưu trữ vĩnh viễn trên blockchain và xác định trạng thái của hợp đồng.
    uint256 public addProfitCounter;
    uint256 public campaignCounter;
    uint256 public requestWithdrawCounter;
    uint256 public constant MIN_INVESTMENT_AMOUNT = 0.01 ether;
    uint256 public constant VOTE_DURATION = 3 days;
    uint16  public fees;
    address public feeReceiver;

    // --- MAPPINGS ---
    // Mappings hoạt động như các bảng băm (hash tables), dùng để lưu trữ và truy xuất dữ liệu một cách hiệu quả.
    mapping(uint256 => Campaign) public campaigns;
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
    // Events dùng để phát ra các tín hiệu mà các ứng dụng bên ngoài (frontend, server) có thể lắng nghe. Rất quan trọng cho UI/UX.
    event CampaignCreated(uint256 indexed id, address indexed owner, string name, uint256 goalAmount, uint256 endTime);
    event InvestmentReceived(uint256 indexed campaignId, address indexed investor, uint256 amount, uint256 currentRaisedAmount);
    event WithdrawalRequested(uint256 indexed campaignId, uint256 indexed requestId, address indexed requester, uint256 amount, string reason, uint256 voteEndTime);
    event VoteCast(uint256 indexed campaignId, uint256 indexed requestId, address indexed voter, bool agree, uint256 voteWeight);
    event WithdrawalExecuted(WithdrawalStatus status,uint256 indexed campaignId, uint256 indexed requestId, address indexed recipient, uint256 amount);
    event RefundIssued(uint256 indexed campaignId, address indexed investor, uint256 amount);
    event CampaignStatusUpdated(uint256 indexed campaignId, uint8 newStatus);
    event ProfitAdded(uint256 indexed id,uint256 indexed campaignId, uint256 amount);
    event ProfitClaimed(uint256 indexed campaignId, address indexed investor, uint256 amount);
    event CampaignCanceledByAdmin(uint256 indexed campaignId, address indexed admin);
    event WithdrawalRequestCanceled(uint256 indexed campaignId, uint256 indexed requestId);
    event SetFees(address indexed receiver, uint16 fees);
    event SetAddressReceiver(address indexed receiver, address indexed admin);

    // --- MODIFIERS ---
    // Modifiers là các "bộ lọc" có thể tái sử dụng để kiểm tra các điều kiện trước khi một hàm được thực thi.
    modifier onlyAdmin() {
        // Kiểm tra xem người gọi hàm (msg.sender) có vai trò ADMIN_ROLE hay không.
        require(hasRole(ADMIN_ROLE, msg.sender), "Caller is not an admin");
        _; // Ký hiệu cho phép phần thân của hàm được thực thi nếu điều kiện trên đúng.
    }

    // modifier onlyCreator() {
    //     // Kiểm tra xem người gọi hàm có vai trò CREATOR_ROLE hay không.
    //     require(hasRole(CREATOR_ROLE, msg.sender), "Caller is not a creator");
    //     _;
    // }
    
    modifier onlyCampaignOwner(uint256 _campaignId) {
        // Kiểm tra xem người gọi hàm có phải là chủ sở hữu của chiến dịch cụ thể này không.
        require(campaigns[_campaignId].owner == msg.sender, "Not campaign owner");
        _;
    }
    
    // --- CONSTRUCTOR ---
    // Hàm constructor là hàm đặc biệt, chỉ chạy một lần duy nhất khi hợp đồng được triển khai (deploy).
    constructor() {
        // 1. Cấp vai trò DEFAULT_ADMIN_ROLE cho người triển khai hợp đồng (msg.sender).
        // Đây là vai trò cao nhất, có quyền quản lý các vai trò khác.
        _grantRole(DEFAULT_ADMIN_ROLE, msg.sender);
        
        // 2. Cấp luôn vai trò ADMIN_ROLE và CREATOR_ROLE cho người triển khai để tiện sử dụng ban đầu.
        _grantRole(ADMIN_ROLE, msg.sender);
        _grantRole(CREATOR_ROLE, msg.sender);

        // 3. Thiết lập hệ thống phân cấp quyền quản trị cho các vai trò.
        // Quy định rằng chỉ có DEFAULT_ADMIN_ROLE mới có quyền thêm/xóa thành viên của ADMIN_ROLE.
        _setRoleAdmin(ADMIN_ROLE, DEFAULT_ADMIN_ROLE);
        // Quy định rằng chỉ có ADMIN_ROLE mới có quyền thêm/xóa thành viên của CREATOR_ROLE.
        // _setRoleAdmin(CREATOR_ROLE, ADMIN_ROLE);
        // Quy định rằng chỉ có ADMIN_ROLE mới có quyền thêm/xóa thành viên của ALLOWED_ROLE.
    }

    // --- CÁC HÀM QUẢN LÝ VAI TRÒ (DÀNH CHO ADMIN) ---
    function grantAdmin(address _user) external {
        // Chỉ người có vai trò DEFAULT_ADMIN_ROLE (thường là người deploy) mới được thêm Admin mới.
        require(hasRole(DEFAULT_ADMIN_ROLE, msg.sender), "Caller is not the default admin");
        grantRole(ADMIN_ROLE, _user);
    }

    function revokeAdmin(address _user) external {
        // Tương tự, chỉ DEFAULT_ADMIN_ROLE mới được thu hồi quyền Admin.
        require(hasRole(DEFAULT_ADMIN_ROLE, msg.sender), "Caller is not the default admin");
        revokeRole(ADMIN_ROLE, _user);
    }

    // function grantCreator(address _user) external onlyAdmin {
    //     // Chỉ Admin mới được cấp quyền Creator.
    //     grantRole(CREATOR_ROLE, _user);
    // }

    // function revokeCreator(address _user) external onlyAdmin {
    //     // Chỉ Admin mới được thu hồi quyền Creator.
    //     revokeRole(CREATOR_ROLE, _user);
    // }

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

    // --- CÁC HÀM CHỨC NĂNG CỦA HỢP ĐỒNG ---
    function cancelCampaignByAdmin(uint256 _campaignId) external onlyAdmin {
        Campaign storage campaign = campaigns[_campaignId];
        // Chỉ có thể hủy các chiến dịch đang trong trạng thái 'Active'.
        require(campaign.status == CampaignStatus.Active, "Only active campaigns can be canceled");
        // Chuyển trạng thái thành 'Failed' để cho phép các nhà tài trợ được hoàn tiền.
        campaign.status = CampaignStatus.Failed;
        emit CampaignCanceledByAdmin(_campaignId, msg.sender);
    }

    function createCampaign(
        string memory _name,
        uint256 _goalAmount,
        uint256 _durationInDays,
        address _creatorAddress
    ) external onlyAdmin {
        require(_goalAmount > 0, "Goal must be greater than 0");
        require(_durationInDays > 0, "Duration must be greater than 0");

        uint256 endTime = block.timestamp + _durationInDays * 1 days; 
        campaignCounter++;
        
        // Tạo một thực thể Campaign mới và lưu vào mapping `campaigns`.
        campaigns[campaignCounter] = Campaign({
            id: campaignCounter,
            owner: _creatorAddress,
            name: _name,
            goalAmount: _goalAmount,
            currentRaisedAmount: 0,
            totalRaisedAmount: 0,
            endTime: endTime,
            status: CampaignStatus.Active,
            investorCount: 0
        });
        emit CampaignCreated(campaignCounter, _creatorAddress, _name, _goalAmount, endTime);
    }
    
    // Hàm này là `payable`, có nghĩa là nó có thể nhận ETH khi được gọi.
    function invest(uint256 _campaignId) external payable nonReentrant {
        Campaign storage campaign = campaigns[_campaignId];
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign does not exist");
        require(campaign.status == CampaignStatus.Active, "Campaign is not active");
        require(msg.value >= MIN_INVESTMENT_AMOUNT, "investment amount is too small");
        require(block.timestamp < campaign.endTime, "Campaign has ended");

        // Nếu đây là lần đầu người này quyên góp, tăng số lượng nhà tài trợ lên.
        if (campaignInvestorBalances[_campaignId][msg.sender] == 0) {
            campaign.investorCount++;
            campaignInvestors[_campaignId].push(msg.sender);
        }
        
        // Cập nhật các số liệu liên quan.
        campaignInvestorBalances[_campaignId][msg.sender] += msg.value;
        campaign.currentRaisedAmount += msg.value;
        
        // Lưu lại lịch sử của lần quyên góp này.
        campaignInvestments[_campaignId].push(Investment({
            investor: msg.sender,
            amount: msg.value,
            timestamp: block.timestamp
        }));
        emit InvestmentReceived(_campaignId, msg.sender, msg.value, campaign.currentRaisedAmount);
    }
    
    function addProfit(uint256 _campaignId) external payable onlyCampaignOwner(_campaignId) {
        require(campaigns[_campaignId].status == CampaignStatus.Completed, "Campaign not completed");
        require(msg.value > 0, "Profit must be greater than zero");
        uint256 newProfitId = addProfitCounter++;
        // Tạo một đối tượng Profit mới và thêm vào mảng tổng lợi nhuận.
        listProfits[_campaignId].push(Profit({
            id: newProfitId,
            campaignId: _campaignId,
            amount: msg.value
        }));
        // Cộng dồn lợi nhuận vào tổng lợi nhuận của chiến dịch.
        totalProfits[_campaignId] += msg.value;
        emit ProfitAdded(newProfitId, _campaignId, msg.value);
    }

    function claimProfit(uint256 _campaignId, uint256 _profitIndex) external nonReentrant {
        // Đổi tên _profitId thành _profitIndex để rõ ràng hơn
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign does not exist");

        Campaign storage campaign = campaigns[_campaignId];
        require(campaign.status == CampaignStatus.Completed, "Campaign not completed");
        // ✅ FIX: Use totalRaisedAmount for the check as currentRaisedAmount might be 0 after withdrawal
        require(campaign.totalRaisedAmount > 0, "Invalid total investments for profit calculation");

        uint256 userInvestment = campaignInvestorBalances[_campaignId][msg.sender];
        require(userInvestment > 0, "No investments found for this user");

        // Đảm bảo rằng _profitIndex không vượt quá số lượng lợi nhuận đã thêm vào.
        require(_profitIndex < listProfits[_campaignId].length, "Profit index out of bounds");
        Profit storage profit = listProfits[_campaignId][_profitIndex];

        require(profit.amount > 0, "No profit to claim");

        // Sử dụng _profitIndex để kiểm tra việc đã nhận
        require(!profitClaimed[_campaignId][_profitIndex][msg.sender], "Profit already claimed");

        uint256 profitShare = (profit.amount * userInvestment) / campaign.totalRaisedAmount;
        require(profitShare > 0, "Profit share is too small to claim");

        // Đánh dấu là đã nhận trước khi gửi tiền (Checks-Effects-Interactions)
        profitClaimed[_campaignId][_profitIndex][msg.sender] = true;

        (bool sent, ) = msg.sender.call{value: profitShare}("");
        require(sent, "Profit transfer failed");

        emit ProfitClaimed(_campaignId, msg.sender, profitShare);
    }

    function voteForWithdrawal(uint256 _campaignId, uint256 _requestId, bool _agree) external {
        // Trọng số phiếu bầu bằng tổng số tiền người đó đã quyên góp.
        uint256 voteWeight = campaignInvestorBalances[_campaignId][msg.sender];
        require(voteWeight > 0, "Only investors can vote");
        require(_requestId < withdrawalRequests[_campaignId].length, "Withdrawal request not found");
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign does not exist");

        WithdrawalRequest storage request = withdrawalRequests[_campaignId][_requestId];
        
        // Kiểm tra xem phiên vote còn trong thời hạn hay không.
        require(block.timestamp <= request.voteEndTime, "Voting period has ended");
        
        require(request.status == WithdrawalStatus.Pending, "Request is not pending");
        require(!withdrawalVotes[_campaignId][_requestId][msg.sender], "You have already voted");
        
        // Đánh dấu người này đã vote.
        withdrawalVotes[_campaignId][_requestId][msg.sender] = true;
        
        // Cộng trọng số phiếu vào phe tương ứng.
        if (_agree) {
            request.agreeVotes += voteWeight;
        } else {
            request.disagreeVotes += voteWeight;
        }
        
        emit VoteCast(_campaignId, _requestId, msg.sender, _agree, voteWeight);
    }

    // Hàm này có thể được gọi bởi bất kỳ ai để tăng tính phi tập trung.
    function checkAndExecuteWithdrawal(uint256 _campaignId, uint256 _requestId) external nonReentrant {
        // === Checks ===
        Campaign storage campaign = campaigns[_campaignId];
        require(_requestId < withdrawalRequests[_campaignId].length, "Withdrawal request not found");
        WithdrawalRequest storage request = withdrawalRequests[_campaignId][_requestId];

        require(request.status == WithdrawalStatus.Pending, "Request not pending");
        require(block.timestamp > request.voteEndTime, "Voting period not yet ended");

        if (request.agreeVotes > campaign.totalRaisedAmount / 2) {
            uint256 feeAmount = (request.amount * fees) / 100;
            uint256 amountToTransfer = request.amount - feeAmount;
            require(campaign.currentRaisedAmount >= request.amount, "Insufficient funds in campaign");

            // === Effects ===
            // Cập nhật tất cả trạng thái TRƯỚC KHI gửi tiền
            request.status = WithdrawalStatus.Executed;
            campaign.currentRaisedAmount -= request.amount;
            campaign.status = CampaignStatus.Completed;

            if (fees > 0 && feeAmount > 0) {
                withdrawWithFees[feeReceiver][_campaignId] += feeAmount;
                totalWithdrawFees[feeReceiver] += feeAmount;
            }
            
            emit WithdrawalExecuted(WithdrawalStatus.Executed, _campaignId, _requestId, request.requester, amountToTransfer);
            emit CampaignStatusUpdated(_campaignId, uint8(CampaignStatus.Completed));
            
            // === Interactions ===
            // Gửi tiền cho chủ chiến dịch SAU CÙNG
            (bool sent, ) = request.requester.call{value: amountToTransfer}("");
            require(sent, "Failed to send Ether to requester");

            // Gửi phí cho feeReceiver SAU CÙNG
            if (fees > 0 && feeAmount > 0) {
                (bool feeSent, ) = feeReceiver.call{value: feeAmount}("");
                require(feeSent, "Failed to send fees");
            }

        } else {
            // === Effects for failed vote ===
            request.status = WithdrawalStatus.Canceled;
            getDenialsRequestedWithDrawCampaigns[_campaignId]++;
            emit WithdrawalExecuted(WithdrawalStatus.Canceled, _campaignId, _requestId, request.requester, 0);
        }
    }
    
    // function cancelWithdrawalRequest(uint256 _campaignId, uint256 _requestId) external {
    //     require(_requestId < withdrawalRequests[_campaignId].length, "Withdrawal request not found");
    //     WithdrawalRequest storage request = withdrawalRequests[_campaignId][_requestId];
    //     // Chỉ chủ chiến dịch hoặc admin mới có quyền hủy yêu cầu.
    //     require(msg.sender == request.requester || hasRole(ADMIN_ROLE, msg.sender), "Not authorized to cancel");
    //     require(request.status == WithdrawalStatus.Pending, "Request is not pending");
    //     // Phải quá hạn vote thì mới được hủy.
    //     require(block.timestamp > request.voteEndTime, "Voting period has not ended yet");
    //     // Cập nhật trạng thái yêu cầu.
    //     request.status = WithdrawalStatus.Canceled;
    //     emit WithdrawalRequestCanceled(_campaignId, _requestId);
    // }

    function refund(uint256 _campaignId) external nonReentrant {
        Campaign storage campaign = campaigns[_campaignId];
        require(campaign.status == CampaignStatus.Failed, "Campaign did not fail");
        
        uint256 refundAmount = campaignInvestorBalances[_campaignId][msg.sender];
        require(refundAmount > 0, "No investment to refund");
        
        // Đặt lại số dư về 0 trước khi gửi tiền để chống tấn công Tái nhập.
        campaignInvestorBalances[_campaignId][msg.sender] = 0;
        
        (bool sent, ) = msg.sender.call{value: refundAmount}("");
        require(sent, "Refund failed");
        
        emit RefundIssued(_campaignId, msg.sender, refundAmount);
    }

    // Hàm này có thể được gọi bởi bất kỳ ai để "đánh thức" hợp đồng cập nhật trạng thái.
    function updateCampaignStatus(uint256 _campaignId) public {
        Campaign storage campaign = campaigns[_campaignId];

        // Chỉ cập nhật nếu đang ở trạng thái Active hoặc Voting
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

    function requestFullWithdrawal(uint256 _campaignId, string memory _reason) external onlyCampaignOwner(_campaignId) {
        updateCampaignStatus(_campaignId); // Tự động cập nhật trước khi chạy logic chính
        Campaign storage campaign = campaigns[_campaignId];

        require(campaign.currentRaisedAmount > 0, "No funds to withdraw");
        require(getDenialsRequestedWithDrawCampaigns[_campaignId] < 3, "Too many withdrawal requests denied");
        require(campaign.status == CampaignStatus.Voting, "Campaign is not in voting status");
        require(campaign.currentRaisedAmount >= campaign.goalAmount, "Insufficient funds in campaign");

        uint256 requestCount = withdrawalRequests[_campaignId].length;
        if (requestCount > 0) {
            WithdrawalRequest storage lastRequest = withdrawalRequests[_campaignId][requestCount - 1];
            require(
                lastRequest.status == WithdrawalStatus.Canceled,
                "Previous withdrawal request is still active"
            );
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
}