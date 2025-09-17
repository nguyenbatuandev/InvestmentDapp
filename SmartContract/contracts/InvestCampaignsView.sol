// SPDX-License-Identifier: MIT
pragma solidity ^0.8.30;

// Dùng alias để IDE/Plugin không hiểu nhầm định danh
import { InvestCampaigns as InvestCampaignsBase } from "./InvestCampaigns.sol";

/**
 * @title InvestCampaignsView
 * @dev View mở rộng, chỉ có hàm đọc. Kế thừa từ InvestCampaigns để truy cập state.
 */
contract InvestCampaignsView is InvestCampaignsBase {
    // Base constructor không có tham số => không cần gọi tường minh
    constructor() {}

    // ------------ Helpers ------------
    function _exists(uint256 _campaignId) internal view returns (bool) {
        return campaigns[_campaignId].owner != address(0);
    }

    // ------------ TRUY VẤN CHIẾN DỊCH ------------
    function getCampaignDetails(uint256 _campaignId) external view returns (Campaign memory) {
        require(_exists(_campaignId), "Campaign not found");
        return campaigns[_campaignId];
    }

    function getAllCampaigns(uint256 _offset, uint256 _limit) external view returns (Campaign[] memory) {
        uint256 totalReal = 0;
        for (uint256 i = 1; i <= campaignCounter; i++) {
            if (_exists(i)) totalReal++;
        }
        if (_offset >= totalReal) return new Campaign[](0);

        uint256 size = totalReal - _offset;
        if (size > _limit) size = _limit;

        Campaign[] memory result = new Campaign[](size);
        uint256 seen = 0;
        uint256 filled = 0;
        for (uint256 i = 1; i <= campaignCounter && filled < size; i++) {
            if (_exists(i)) {
                if (seen++ >= _offset) {
                    result[filled++] = campaigns[i];
                }
            }
        }
        return result;
    }

    function getCampaignsByStatus(
        CampaignStatus _status,
        uint256 _offset,
        uint256 _limit
    ) external view returns (Campaign[] memory) {
        uint256 total = 0;
        for (uint256 i = 1; i <= campaignCounter; i++) {
            if (_exists(i) && campaigns[i].status == _status) total++;
        }
        if (_offset >= total) return new Campaign[](0);

        uint256 size = total - _offset;
        if (size > _limit) size = _limit;

        Campaign[] memory result = new Campaign[](size);
        uint256 seen = 0;
        uint256 filled = 0;
        for (uint256 i = 1; i <= campaignCounter && filled < size; i++) {
            if (_exists(i) && campaigns[i].status == _status) {
                if (seen++ >= _offset) {
                    result[filled++] = campaigns[i];
                }
            }
        }
        return result;
    }

    function getCampaignsByCreator(address _creator) external view returns (uint256[] memory) {
        uint256 count = 0;
        for (uint256 i = 1; i <= campaignCounter; i++) {
            if (_exists(i) && campaigns[i].owner == _creator) count++;
        }
        uint256[] memory ids = new uint256[](count);
        uint256 idx = 0;
        for (uint256 i = 1; i <= campaignCounter && idx < count; i++) {
            if (_exists(i) && campaigns[i].owner == _creator) {
                ids[idx++] = campaigns[i].id;
            }
        }
        return ids;
    }

    // ------------ TRUY VẤN TƯƠNG TÁC ------------
    function getinvestmentsForCampaign(
        uint256 _campaignId,
        uint256 _offset,
        uint256 _limit
    ) external view returns (Investment[] memory) {
        Investment[] storage list = campaignInvestments[_campaignId];
        uint256 total = list.length;
        if (_offset >= total) return new Investment[](0);

        uint256 count = total - _offset;
        if (count > _limit) count = _limit;

        Investment[] memory result = new Investment[](count);
        for (uint256 i = 0; i < count; i++) {
            result[i] = list[_offset + i];
        }
        return result;
    }

    function getWithdrawalRequestsForCampaign(
        uint256 _campaignId,
        uint256 _offset,
        uint256 _limit
    ) external view returns (WithdrawalRequest[] memory) {
        WithdrawalRequest[] storage list = withdrawalRequests[_campaignId];
        uint256 total = list.length;
        if (_offset >= total) return new WithdrawalRequest[](0);

        uint256 count = total - _offset;
        if (count > _limit) count = _limit;

        WithdrawalRequest[] memory result = new WithdrawalRequest[](count);
        for (uint256 i = 0; i < count; i++) {
            result[i] = list[_offset + i];
        }
        return result;
    }

    function getInvestorBalanceForCampaign(uint256 _campaignId, address _investor) external view returns (uint256) {
        return campaignInvestorBalances[_campaignId][_investor];
    }

    function hasVoted(uint256 _campaignId, uint256 _requestId, address _voter) external view returns (bool) {
        return withdrawalVotes[_campaignId][_requestId][_voter];
    }

    // ------------ TRUY VẤN LỢI NHUẬN ------------
    function getTotalProfitForCampaign(uint256 _campaignId) external view returns (uint256) {
        return totalProfits[_campaignId];
    }

    function hasClaimedProfit(uint256 _campaignId, uint256 _profitIndex, address _investor) external view returns (bool) {
        return profitClaimed[_campaignId][_profitIndex][_investor];
    }

    function getProfitClaimStatuses(
        uint256 _campaignId,
        uint256 _profitIndex,
        address[] memory _investors
    ) external view returns (bool[] memory) {
        require(_exists(_campaignId), "Campaign not found");
        require(_profitIndex < listProfits[_campaignId].length, "Profit index out of bounds");

        uint256 n = _investors.length;
        bool[] memory statuses = new bool[](n);
        for (uint256 i = 0; i < n; i++) {
            statuses[i] = profitClaimed[_campaignId][_profitIndex][_investors[i]];
        }
        return statuses;
    }

    function getProfitClaimStatuses(
        uint256 _campaignId,
        address[] memory _investors
    ) external view returns (bool[] memory) {
        require(_exists(_campaignId), "Campaign not found");

        uint256 profitCount = listProfits[_campaignId].length;
        uint256 n = _investors.length;

        bool[] memory statuses = new bool[](profitCount * n);
        for (uint256 i = 0; i < profitCount; i++) {
            for (uint256 j = 0; j < n; j++) {
                statuses[i * n + j] = profitClaimed[_campaignId][i][_investors[j]];
            }
        }
        return statuses;
    }

    // ------------ NỀN TẢNG & VAI TRÒ ------------
    function getPlatformStats() external view returns (
        uint256 totalCampaigns_,
        uint256 totalActive,
        uint256 totalCompleted,
        uint256 totalFailed,
        uint256 totalFundsRaised
    ) {
        for (uint256 i = 1; i <= campaignCounter; i++) {
            if (!_exists(i)) continue;
            totalCampaigns_++;
            Campaign storage c = campaigns[i];
            if (c.status == CampaignStatus.Active) totalActive++;
            else if (c.status == CampaignStatus.Completed) totalCompleted++;
            else if (c.status == CampaignStatus.Failed) totalFailed++;
            totalFundsRaised += c.currentRaisedAmount;
        }
        return (totalCampaigns_, totalActive, totalCompleted, totalFailed, totalFundsRaised);
    }

    function isAdmin(address _user) external view returns (bool) {
        return hasRole(ADMIN_ROLE, _user);
    }

    function isCreator(address _user) external view returns (bool) {
        return hasRole(CREATOR_ROLE, _user);
    }
}
