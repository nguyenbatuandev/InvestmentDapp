// SPDX-License-Identifier: MIT
pragma solidity ^0.8.30;

// Import hợp đồng chính để kế thừa toàn bộ trạng thái và logic nội bộ.
import "./InvestCampaigns.sol";

/**
 * @title InvestCampaignsExtendedView
 * @dev Phiên bản view mở rộng, cung cấp các hàm truy vấn nâng cao về lọc và thống kê.
 * Kế thừa toàn bộ trạng thái từ InvestCampaigns nhưng chỉ chứa các hàm không thay đổi dữ liệu.
 */
contract InvestCampaignsView is InvestCampaigns {

    /**
     * @dev Constructor chỉ cần gọi constructor của hợp đồng cha.
     */
    constructor() InvestCampaigns() {}

    // =================================================================
    // == CÁC HÀM TRUY VẤN (VIEW FUNCTIONS)
    // =================================================================

    // --- TRUY VẤN CHIẾN DỊCH ---

    /**
     * @notice Lấy thông tin chi tiết của một chiến dịch dựa trên ID.
     * @param _campaignId ID của chiến dịch cần truy vấn.
     * @return Campaign struct chứa toàn bộ dữ liệu của chiến dịch.
     */
    function getCampaignDetails(uint256 _campaignId) external view returns (Campaign memory) {
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign ID is invalid");
        return campaigns[_campaignId];
    }

    /**
     * @notice Lấy một danh sách các chiến dịch với hỗ trợ phân trang.
     * @param _offset Vị trí bắt đầu lấy dữ liệu trong danh sách toàn bộ chiến dịch.
     * @param _limit Số lượng tối đa các chiến dịch cần trả về.
     * @return Một mảng các Campaign struct.
     */
    function getAllCampaigns(uint256 _offset, uint256 _limit) external view returns (Campaign[] memory) {
        uint256 total = campaignCounter;
        if (_offset >= total) {
            return new Campaign[](0);
        }

        uint256 count = total - _offset > _limit ? _limit : total - _offset;
        Campaign[] memory result = new Campaign[](count);

        for (uint256 i = 0; i < count; i++) {
            // ID chiến dịch bắt đầu từ 1, nên cần +1
            result[i] = campaigns[_offset + i + 1];
        }
        return result;
    }

    /**
     * @notice Lấy các chiến dịch theo trạng thái cụ thể (Active, Completed, Failed), có phân trang.
     */
    function getCampaignsByStatus(CampaignStatus _status, uint256 _offset, uint256 _limit) external view returns (Campaign[] memory) {
        uint256 total = campaignCounter;
        uint256[] memory tempIds = new uint256[](total);
        uint256 count = 0;
        
        // Vòng lặp đầu tiên để thu thập ID của các chiến dịch hợp lệ.
        for (uint256 i = 1; i <= total; i++) {
            if (campaigns[i].status == _status) {
                tempIds[count] = i;
                count++;
            }
        }

        if (_offset >= count) {
            return new Campaign[](0);
        }

        uint256 size = count - _offset > _limit ? _limit : count - _offset;
        Campaign[] memory result = new Campaign[](size);

        // Vòng lặp thứ hai để xây dựng mảng kết quả từ các ID đã lọc.
        for (uint256 i = 0; i < size; i++) {
            result[i] = campaigns[tempIds[_offset + i]];
        }
        return result;
    }

    /**
     * @notice Lấy danh sách các ID chiến dịch của một người tạo cụ thể.
     */
    function getCampaignsByCreator(address _creator) external view returns (uint256[] memory) {
        uint256 total = campaignCounter;
        uint256[] memory tempCampaigns = new uint256[](total);
        uint256 count = 0;
        
        for (uint256 i = 1; i <= total; i++) {
            if (campaigns[i].owner == _creator) {
                tempCampaigns[count] = campaigns[i].id;
                count++;
            }
        }

        uint256[] memory result = new uint256[](count);
        for (uint256 i = 0; i < count; i++) {
            result[i] = tempCampaigns[i];
        }
        return result;
    }

    // --- TRUY VẤN TƯƠNG TÁC NGƯỜI DÙNG ---

    /**
     * @notice Lấy lịch sử quyên góp của một chiến dịch (có phân trang).
     */
    function getinvestmentsForCampaign(uint256 _campaignId, uint256 _offset, uint256 _limit) external view returns (Investment[] memory) {
        Investment[] storage investmentsList = campaignInvestments[_campaignId];
        uint256 total = investmentsList.length;

        if (_offset >= total) {
            return new Investment[](0);
        }

        uint256 count = total - _offset > _limit ? _limit : total - _offset;
        Investment[] memory result = new Investment[](count);

        for (uint256 i = 0; i < count; i++) {
            result[i] = investmentsList[_offset + i];
        }
        return result;
    }

    /**
     * @notice Lấy danh sách các yêu cầu rút tiền của một chiến dịch (có phân trang).
     */
    function getWithdrawalRequestsForCampaign(uint256 _campaignId, uint256 _offset, uint256 _limit) external view returns (WithdrawalRequest[] memory) {
        WithdrawalRequest[] storage requestsList = withdrawalRequests[_campaignId];
        uint256 total = requestsList.length;

        if (_offset >= total) {
            return new WithdrawalRequest[](0);
        }
        
        uint256 count = total - _offset > _limit ? _limit : total - _offset;
        WithdrawalRequest[] memory result = new WithdrawalRequest[](count);

        for (uint256 i = 0; i < count; i++) {
            result[i] = requestsList[_offset + i];
        }
        return result;
    }

    /**
     * @notice Lấy tổng số tiền một nhà tài trợ đã quyên góp cho một chiến dịch.
     */
    function getInvestorBalanceForCampaign(uint256 _campaignId, address _investor) external view returns (uint256) {
        return campaignInvestorBalances[_campaignId][_investor];
    }
    
    /**
     * @notice Kiểm tra xem một người đã bỏ phiếu cho một yêu cầu rút tiền hay chưa.
     */
    function hasVoted(uint256 _campaignId, uint256 _requestId, address _voter) external view returns (bool) {
        return withdrawalVotes[_campaignId][_requestId][_voter];
    }

    // --- TRUY VẤN LỢI NHUẬN ---

    /**
     * @notice Lấy tổng lợi nhuận của một chiến dịch.
     */
    function getTotalProfitForCampaign(uint256 _campaignId) external view returns (uint256) {
        
        return totalProfits[_campaignId];
    }

    /**
     * @notice Kiểm tra xem một nhà tài trợ đã nhận lợi nhuận từ chiến dịch chưa.
     */
    function hasClaimedProfit(uint256 _campaignId, uint256 _profitId, address _investor) external view returns (bool) {
        return profitClaimed[_campaignId][_profitId][_investor];
    }

    // --- TRUY VẤN NỀN TẢNG VÀ VAI TRÒ ---
    
    /**
     * @notice Lấy các số liệu thống kê tổng quan của toàn bộ nền tảng.
     */
    function getPlatformStats() external view returns (
        uint256 totalCampaigns,
        uint256 totalActive,
        uint256 totalCompleted,
        uint256 totalFailed,
        uint256 totalFundsRaised
    ) {
        totalCampaigns = campaignCounter;
        uint256 totalRaised = 0;

        for (uint256 i = 1; i <= totalCampaigns; i++) {
            Campaign storage campaign = campaigns[i];
            if (campaign.status == CampaignStatus.Active) {
                totalActive++;
            } else if (campaign.status == CampaignStatus.Completed) {
                totalCompleted++;
            } else if (campaign.status == CampaignStatus.Failed) {
                totalFailed++;
            }
            totalRaised += campaigns[i].currentRaisedAmount;
        }
        
        return (totalCampaigns, totalActive, totalCompleted, totalFailed, totalRaised);
    }

    /**
     * @notice Kiểm tra xem một người dùng có phải là Admin hay không.
     */
    function isAdmin(address _user) external view returns (bool) {
        return hasRole(ADMIN_ROLE, _user);
    }

    /**
     * @notice Kiểm tra xem một người dùng có phải là Creator hay không.
     */
    function isCreator(address _user) external view returns (bool) {
        return hasRole(CREATOR_ROLE, _user);
    }


    function getProfitClaimStatuses(
        uint256 _campaignId,
        uint256 _profitIndex,
        address[] memory _investors
    ) external view returns (bool[] memory) {
        // --- KIỂM TRA ĐẦU VÀO ---
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign ID is invalid");
        require(_profitIndex < listProfits[_campaignId].length, "Profit index out of bounds");

        // --- LOGIC CHÍNH ---
        uint256 investorCount = _investors.length;
        bool[] memory statuses = new bool[](investorCount);

        // Duyệt qua mảng địa chỉ đầu vào và truy vấn trực tiếp từ mapping `profitClaimed`.
        for (uint256 i = 0; i < investorCount; i++) {
            statuses[i] = profitClaimed[_campaignId][_profitIndex][_investors[i]];
        }

        return statuses;
    }

    function getProfitClaimStatuses (
        uint256 _campaignId,
        address[] memory _investors
    ) external view returns (bool[] memory) {
        // --- KIỂM TRA ĐẦU VÀO ---
        require(_campaignId > 0 && _campaignId <= campaignCounter, "Campaign ID is invalid");

        // --- LOGIC CHÍNH ---
        uint256 profitCount = listProfits[_campaignId].length;
        bool[] memory statuses = new bool[](profitCount * _investors.length);

        for (uint256 i = 0; i < profitCount; i++) {
            for (uint256 j = 0; j < _investors.length; j++) {
                statuses[i * _investors.length + j] = profitClaimed[_campaignId][i][_investors[j]];
            }
        }

        return statuses;
    }

}