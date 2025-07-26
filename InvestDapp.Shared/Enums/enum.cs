using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Enums
{
    public enum CampaignStatus
    {
        Active,
        Voting,
        Completed,
        Failed
    }

    public enum WithdrawalStatus
    {
        Pending,
        Executed,
        Canceled
    }
    // Enums.cs
    public enum ConversationType
    {
        Private, // 1-1
        Group    // Nhóm
    }

    public enum ParticipantRole
    {
        Admin,
        Member
    }

    public enum MessageType
    {
        Text,
        Image,
        File
    }
}
