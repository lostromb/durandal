using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security
{
    public class ClientIdentifier
    {
        public string ClientId { get; set; }
        public string UserId { get; set; }
        public string ClientName { get; set; }
        public string UserName { get; set; }

        public ClientIdentifier() : this(null, null, null, null) { }

        public ClientIdentifier(string userId, string userName, string clientId, string clientName)
        {
            UserId = userId;
            UserName = userName;
            ClientId = clientId;
            ClientName = clientName;
        }

        // IMPORTANT: We do not consider ClientName / UserName in tests for equality.
        // As long as the IDs are the same, they are considered to be identical
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is ClientIdentifier))
                return false;

            ClientIdentifier other = obj as ClientIdentifier;
            return string.Equals(ClientId, other.ClientId) &&
                string.Equals(UserId, other.UserId);
        }

        public override int GetHashCode()
        {
            return (ClientId == null ? 90827831 : ClientId.GetHashCode()) +
                (UserId == null ? 8948925 : UserId.GetHashCode());
        }

        public override string ToString()
        {
             return "ClientId=" + ClientId + "/UserId=" + UserId;
        }
        
        public ClientKeyIdentifier GetKeyIdentifier(ClientAuthenticationScope scope)
        {
            if (scope == ClientAuthenticationScope.Client)
            {
                return new ClientKeyIdentifier(scope, clientId: ClientId);
            }
            else if (scope == ClientAuthenticationScope.User)
            {
                return new ClientKeyIdentifier(scope, userId: UserId);
            }
            else if (scope == ClientAuthenticationScope.UserClient)
            {
                return new ClientKeyIdentifier(scope, userId: UserId, clientId: ClientId);
            }
            else
            {
                throw new ArgumentException("Invalid scope");
            }
        }

        public string UserIdScheme
        {
            get
            {
                int pos = UserId.IndexOf(':');
                return pos > 0 ? UserId.Substring(0, pos) : null;
            }
        }

        public string UserIdWithoutScheme
        {
            get
            {
                int pos = UserId.IndexOf(':');
                return pos > 0 ? UserId.Substring(pos + 1) : UserId;
            }
        }

        public string ClientIdScheme
        {
            get
            {
                int pos = ClientId.IndexOf(':');
                return pos > 0 ? ClientId.Substring(0, pos) : null;
            }
        }

        public string ClientIdWithoutScheme
        {
            get
            {
                int pos = ClientId.IndexOf(':');
                return pos > 0 ? ClientId.Substring(pos + 1) : ClientId;
            }
        }
    }
}
