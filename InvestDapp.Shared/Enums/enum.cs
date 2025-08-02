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
        Pending,    // Chờ duyệt
        Approved,   // Đã duyệt
        Rejected    // Bị từ chối
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

    // ✅ THÊM ENUM CHO LOẠI BÀI VIẾT
    public enum PostType
    {
        Introduction,   // Giới thiệu dự án
        Update,        // Cập nhật tiến độ
        Achievement,   // Thành tựu đạt được
        Announcement   // Thông báo quan trọng
    }
}
