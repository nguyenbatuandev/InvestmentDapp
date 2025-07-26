using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Application.MessageService
{
    public class UserConnectionManager : IUserConnectionManager
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new ConcurrentDictionary<string, HashSet<string>>();

        public void AddConnection(string userId, string connectionId)
        {
            var connections = UserConnections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (connections)
            {
                connections.Add(connectionId);
            }
        }

        public HashSet<string> GetConnections(string userId)
        {
            UserConnections.TryGetValue(userId, out var connections);
            return connections ?? new HashSet<string>();
        }

        public void RemoveConnection(string userId, string connectionId)
        {
            if (UserConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        UserConnections.TryRemove(userId, out _);
                    }
                }
            }
        }
    }
}
