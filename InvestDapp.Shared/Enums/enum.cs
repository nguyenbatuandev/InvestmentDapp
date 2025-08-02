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

    // ✅ THÊM ENUM CHO TRẠNG THÁI DUYỆT
    public enum ApprovalStatus
    {
        Pending,    
        Approved,  
        Rejected   
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

    public enum PostType
    {
        Introduction,  
        Update,       
        Achievement,  
        Announcement  
    }
}
