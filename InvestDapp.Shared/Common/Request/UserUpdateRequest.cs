using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common.Request
{
    public class UserUpdateRequest
    {
        public string? Email { get; set; }

        public string? Name { get; set; }

        public string? Avatar { get; set; }

        public string? Bio { get; set; }

    }
}
