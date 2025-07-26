using InvestDapp.Application.MessageService;
using InvestDapp.Application.UserService;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Models.Message;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestDapp.Controllers
{
    [Authorize]    
    
    public class ConversationsController : Controller
    {
        private readonly IConversationService _convoService;
        private readonly IUserService _userService;
        private readonly IUser _user;
        
        public IActionResult Index()
        {
            return View();
        }


        public ConversationsController(IConversationService convoService, IUser _user , IUserService userService)
        {
            _convoService = convoService;
            this._user = _user;
            _userService = userService;
        }
        private async Task<int> GetCurrentUserId()
        {
            var wallet = User.FindFirst("WalletAddress")?.Value;
            var id = await _user.GetUserByWalletAddressAsync(wallet);
            return id.ID;

        }

        [HttpPost("Conversations/private")]
        public async Task<IActionResult> StartPrivateConversation([FromBody] StartPrivateChatDto dto)
        {
            try
            {
                // Sửa lỗi deadlock: Dùng await thay vì .Result
                var currentUserId = await GetCurrentUserId();

                // 1. Lấy về đối tượng Conversation đầy đủ từ service
                var conversationEntity = await _convoService.StartPrivateChatAsync(currentUserId, dto.PartnerId);

                if (conversationEntity == null)
                {
                    return BadRequest("Không thể tạo hoặc tìm thấy cuộc hội thoại.");
                }

                // 2. Map đối tượng entity sang ConversationDto mà client cần
                // Tái sử dụng logic map của bạn. Bọc entity vào một List để hàm map có thể chạy.
                var conversationDtos = await _convoService.MapConversationsToDtosAsync(new List<Conversation> { conversationEntity }, currentUserId);
                var conversationDto = conversationDtos.FirstOrDefault();

                if (conversationDto == null)
                {
                    return Problem("Lỗi khi chuyển đổi dữ liệu hội thoại.");
                }

                // 3. Trả về DTO "sạch" cho client
                return Ok(conversationDto);
            }
            catch (Exception ex)
            {
                // Bắt lỗi để debug dễ hơn
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("Conversations/group")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
        {
            // Đã sửa: Dùng await để lấy ID người dùng
            var currentUserId = await GetCurrentUserId();
            var result = await _convoService.CreateGroupAsync(currentUserId, dto.Name, dto.ParticipantIds);
            return Ok(result);
        }

        [HttpPost("Conversations/{conversationId}/participants")]
        public async Task<IActionResult> AddParticipant(int conversationId, [FromBody] AddParticipantDto dto)
        {
            try
            {
                // Đã sửa: Dùng await để lấy ID người dùng
                var currentUserId = await GetCurrentUserId();
                var result = await _convoService.AddMemberToGroupAsync(conversationId, dto.UserId, currentUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUser()
        {
            var wallet = User.FindFirst("WalletAddress").Value;
            var user = await _user.GetUserByWalletAddressAsync(wallet);
            if (user == null) return NotFound();

            // Trả về thông tin cơ bản, không trả về password hash
            return Ok(new { user.ID, user.Name, user.Avatar });
        }

        // File: Controllers/ConversationsController.cs

        [HttpGet]
        public async Task<IActionResult> GetUserConversations()
        {
            var currentUserId = await GetCurrentUserId();
            var conversations = await _convoService.GetUserConversationsServiceAsync(currentUserId);

            var conversationDtos = await _convoService.MapConversationsToDtosAsync(conversations, currentUserId);

            return Ok(conversationDtos);
        }


        // File: Controllers/ConversationsController.cs

        [HttpGet("Conversations/messages/{conversationId}")]
        public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var messages = await _convoService.GetMessagesForConversationAsync(conversationId, pageNumber, pageSize);
            return Ok(messages);
        }

        [HttpGet("User/search")]
        public async Task<IActionResult> SearchUser([FromQuery] string walletAddress)
        {
            // BƯỚC 1: Lấy ID người dùng hiện tại và ĐỢI cho nó xong
            var currentUserId = await GetCurrentUserId(); // Dùng await ở đây

            // BƯỚC 2: Sau khi BƯỚC 1 đã xong, mới bắt đầu tìm người dùng khác
            var user = await _userService.GetUserByWalletAddressAsync(walletAddress);

            if (user == null)
            {
                return NotFound("Không tìm thấy người dùng với địa chỉ ví này.");
            }

            // BƯỚC 3: Bây giờ so sánh hai giá trị đã có sẵn
            if (user.Data.ID == currentUserId)
            {
                return Ok(null);
            }

            var userDto = new
            {
                user.Data.ID,
                user.Data.Name,
                user.Data.Avatar,
                user.Data.WalletAddress,
            };

            return Ok(userDto);
        }

    }
    // Các lớp DTO (Data Transfer Object) để nhận dữ liệu từ client
    public class StartPrivateChatDto { public int PartnerId { get; set; } }
    public class CreateGroupDto { public string Name { get; set; } public List<int> ParticipantIds { get; set; } }
    public class AddParticipantDto { public int UserId { get; set; } }
}
