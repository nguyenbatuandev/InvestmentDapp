using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Application.MessageService
{
    public interface IUserConnectionManager
    {
        void AddConnection(string userId, string connectionId);
        void RemoveConnection(string userId, string connectionId);
        HashSet<string> GetConnections(string userId);
    }
}
