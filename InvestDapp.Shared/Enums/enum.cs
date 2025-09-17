using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Enums
{
    public enum CampaignStatus
    {
        Draft,          // Dự án đang được tạo
        PendingPost,    // Đã tạo dự án, chờ tạo bài viết đầu tiên
        PendingApproval,// Đã có bài viết, chờ admin duyệt
        Active,         // Đã được duyệt, đang gây quỹ
        Voting,         // Đang bỏ phiếu (nếu có tranh chấp)
        Completed,      // Đã hoàn thành thành công
        Failed          // Thất bại hoặc bị từ chối
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
        Pending,    // Đang chờ bỏ phiếu (voting period)
        Approved,   // Được nhà đầu tư chấp thuận, chờ thực thi
        Executed,   // Đã thực thi thành công
        Rejected,   // Bị từ chối bởi nhà đầu tư
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
