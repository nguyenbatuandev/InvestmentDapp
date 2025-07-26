using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common
{
    public class BaseResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public BaseResponse() { }
        public BaseResponse(bool success, string message, T? data = default, List<string>? errors = null)
        {
            Success = success;
            Message = message;
            Data = data;
            Errors = errors;
        }
    }
    public class ResultType
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        // Thêm các dữ liệu khác nếu cần
    }

}