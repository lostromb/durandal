using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Server
{
    public class FileBasedPublicKeyStore : IPublicKeyStore
    {
        private VirtualPath _fileName;
        private IFileSystem _fileSystem;
        private ILogger _logger;

        // we need to maintain a local list of all clients in order for update to work, otherwise
        // we'd have to read the entire file back each time we wanted to change a row
        private IDictionary<ClientKeyIdentifier, ServerSideAuthenticationState> _allClients;
        
        public FileBasedPublicKeyStore(VirtualPath fileName, IFileSystem fileSystem, ILogger logger)
        {
            _fileName = fileName;
            _fileSystem = fileSystem;
            _logger = logger;
            _allClients = new Dictionary<ClientKeyIdentifier, ServerSideAuthenticationState>();
            LoadKnownClients();
        }

        //public async Task<bool> WriteAllClients(IEnumerable<SecureClientInformation> clients)
        //{
        //    _allClients.Clear();
        //    IList<string> clientInfoList = new List<string>();
        //    foreach (var client in clients)
        //    {
        //        _allClients.Add(client.ClientId, client);
        //        clientInfoList.Add(WriteClientInfoLine(client));
        //    }

        //    return await _fileSystem.WriteLinesAsync(_fileName, clientInfoList);
        //}

        public async Task<bool> UpdateClientState(ServerSideAuthenticationState client)
        {
            ClientKeyIdentifier keyId = client.ClientInfo.GetKeyIdentifier(client.KeyScope);
            if (_allClients.ContainsKey(keyId))
            {
                _allClients.Remove(keyId);
            }

            _allClients.Add(keyId, client);
            
            IList<string> clientInfoList = new List<string>();
            foreach (var client2 in _allClients.Values)
            {
                clientInfoList.Add(WriteClientInfoLine(client2));
            }

            await _fileSystem.WriteLinesAsync(_fileName, clientInfoList).ConfigureAwait(false);
            return true;
        }

        public Task<RetrieveResult<ServerSideAuthenticationState>> GetClientState(ClientKeyIdentifier keyId)
        {
            if (_allClients.ContainsKey(keyId))
            {
                return Task.FromResult(new RetrieveResult<ServerSideAuthenticationState>(_allClients[keyId]));
            }

            return Task.FromResult(new RetrieveResult<ServerSideAuthenticationState>());
        }

        public async Task DeleteClientState(ClientKeyIdentifier keyId)
        {
            if (_allClients.ContainsKey(keyId))
            {
                _allClients.Remove(keyId);
            }

            IList<string> clientInfoList = new List<string>();
            foreach (var client2 in _allClients.Values)
            {
                clientInfoList.Add(WriteClientInfoLine(client2));
            }

            await _fileSystem.WriteLinesAsync(_fileName, clientInfoList).ConfigureAwait(false);
        }

        private IDictionary<ClientKeyIdentifier, ServerSideAuthenticationState> LoadKnownClients()
        {
            _allClients.Clear();
            if (_fileSystem.Exists(_fileName))
            {
                _logger.Log("Loading secure client list from " + _fileName);
                IEnumerable<string> clientInfoList = _fileSystem.ReadLines(_fileName);
                int loadedClients = 0;
                foreach (string clientInfoLine in clientInfoList)
                {
                    ServerSideAuthenticationState client = ParseClientInfoLine(clientInfoLine);
                    if (client == null)
                        continue;
                    
                    ClientKeyIdentifier keyId = client.ClientInfo.GetKeyIdentifier(client.KeyScope);
                    _allClients.Add(keyId, client);

                    if (client.Trusted)
                    {
                        _logger.Log("Added \"" + client.ClientInfo.ToString() + "\" as a verified secure client", LogLevel.Std);
                    }
                    else
                    {
                        _logger.Log("Added \"" + client.ClientInfo.ToString() + "\" as an unverified client", LogLevel.Std);
                    }

                    loadedClients++;
                }
                _logger.Log("Loaded " + loadedClients + " known clients");
            }
            else
            {
                _logger.Log("No client list found at " + _fileName + ", assuming no clients are known");
            }

            return _allClients;
        }

        private static string WriteClientInfoLine(ServerSideAuthenticationState client)
        {
            return string.Join("\t",
                string.IsNullOrEmpty(client.ClientInfo.ClientId) ? string.Empty : client.ClientInfo.ClientId,
                string.IsNullOrEmpty(client.ClientInfo.ClientName) ? string.Empty : client.ClientInfo.ClientName,
                string.IsNullOrEmpty(client.ClientInfo.UserId) ? string.Empty : client.ClientInfo.UserId,
                string.IsNullOrEmpty(client.ClientInfo.UserName) ? string.Empty : client.ClientInfo.UserName,
                client.Trusted,
                CryptographyHelpers.SerializeKey(client.PubKey.E),
                CryptographyHelpers.SerializeKey(client.PubKey.N),
                client.PubKey.KeyLengthBits.ToString(),
                client.SaltValue == null ? string.Empty : CryptographyHelpers.SerializeKey(client.SaltValue));
        }

        private static ServerSideAuthenticationState ParseClientInfoLine(string input)
        {
            string[] parts = input.Split('\t');
            if (parts.Length != 11)
            {
                return null;
            }

            ServerSideAuthenticationState returnVal = new ServerSideAuthenticationState();
            returnVal.ClientInfo = new ClientIdentifier(null, null, null, null);
            returnVal.ClientInfo.ClientId = string.IsNullOrEmpty(parts[0]) ? null : parts[0];
            returnVal.ClientInfo.ClientName = string.IsNullOrEmpty(parts[1]) ? null : parts[1];
            returnVal.ClientInfo.UserId = string.IsNullOrEmpty(parts[2]) ? null : parts[2];
            returnVal.ClientInfo.UserName = string.IsNullOrEmpty(parts[3]) ? null : parts[3];
            returnVal.Trusted = bool.Parse(parts[4]);
            returnVal.PubKey = new PublicKey(CryptographyHelpers.DeserializeKey(parts[5]), CryptographyHelpers.DeserializeKey(parts[6]), int.Parse(parts[7]));
            returnVal.SaltValue = string.IsNullOrEmpty(parts[9]) ? null : CryptographyHelpers.DeserializeKey(parts[8]);
            returnVal.KeyScope = ClientAuthenticationScope.None;
            if (!string.IsNullOrEmpty(returnVal.ClientInfo.ClientId))
            {
                returnVal.KeyScope |= ClientAuthenticationScope.Client;
            }
            if (!string.IsNullOrEmpty(returnVal.ClientInfo.UserId))
            {
                returnVal.KeyScope |= ClientAuthenticationScope.User;
            }

            return returnVal;
        }
    }
}
