using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security
{
    public class ClientKeyIdentifier
    {
        public ClientAuthenticationScope Scope { get; set; }
        public string ClientId { get; set; }
        public string UserId { get; set; }

        public ClientKeyIdentifier(ClientAuthenticationScope scope, string userId = null, string clientId = null)
        {
            Scope = scope;
            UserId = userId;
            ClientId = clientId;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is ClientKeyIdentifier))
                return false;

            ClientKeyIdentifier other = obj as ClientKeyIdentifier;
            return Scope == other.Scope &&
                string.Equals(ClientId, other.ClientId) &&
                string.Equals(UserId, other.UserId);
        }

        public override int GetHashCode()
        {
            return Scope.GetHashCode() +
                (ClientId == null ? 90827831 : ClientId.GetHashCode()) +
                (UserId == null ? 7849812 : UserId.GetHashCode());
        }

        public override string ToString()
        {
            if (Scope == ClientAuthenticationScope.Client)
            {
                return "Client/ClientId=" + ClientId;
            }
            else if (Scope == ClientAuthenticationScope.User)
            {
                return "User/UserId=" + UserId;
            }
            else if (Scope == ClientAuthenticationScope.UserClient)
            {
                return "UserClient/ClientId=" + ClientId + "/UserId=" + UserId;
            }
            else
            {
                return Enum.GetName(typeof(ClientAuthenticationScope), Scope + "/UserId=" + UserId + "/ClientId=" + ClientId);
            }
        }
    }
}
