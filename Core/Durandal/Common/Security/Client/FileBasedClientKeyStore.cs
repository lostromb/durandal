using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Security.Login;
using Durandal.Common.Client;
using Newtonsoft.Json;
using System.IO;

namespace Durandal.Common.Security.Client
{
    public class FileBasedClientKeyStore : IClientSideKeyStore
    {
        private static readonly VirtualPath FILE_PATH = new VirtualPath("\\client_keys.json");

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly IDictionary<ClientKeyIdentifier, UserClientSecretInfo> _identities;
        private bool _initialized = false;
        
        public FileBasedClientKeyStore(IFileSystem fileManager, ILogger logger)
        {
            _logger = logger;
            _fileSystem = fileManager;
            _identities = new Dictionary<ClientKeyIdentifier, UserClientSecretInfo>();
        }

        public async Task<bool> StoreIdentity(UserClientSecretInfo identity)
        {
            await InitializeIfNeeded().ConfigureAwait(false);
            ClientKeyIdentifier key = identity.GetKeyId();
            _identities[key] = identity;
            await WriteFile().ConfigureAwait(false);
            return true;
        }

        public async Task<UserClientSecretInfo> LoadIdentity(ClientKeyIdentifier keyId)
        {
            await InitializeIfNeeded().ConfigureAwait(false);
            if (_identities.ContainsKey(keyId))
            {
                return _identities[keyId];
            }

            throw new KeyNotFoundException("No private key found for key ID " + keyId.ToString());
        }

        public async Task<bool> DeleteIdentity(ClientKeyIdentifier keyId)
        {
            await InitializeIfNeeded().ConfigureAwait(false);
            if (_identities.ContainsKey(keyId))
            {
                _identities.Remove(keyId);
                await WriteFile().ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<List<UserIdentity>> GetUserIdentities()
        {
            await InitializeIfNeeded().ConfigureAwait(false);
            List<UserIdentity> returnVal = new List<UserIdentity>();
            foreach (UserClientSecretInfo identity in _identities.Values)
            {
                if (!string.IsNullOrEmpty(identity.UserId))
                {
                    returnVal.Add(new UserIdentity()
                    {
                        AuthProvider = identity.AuthProvider,
                        Id = identity.UserId,
                        FullName = identity.UserFullName,
                        GivenName = identity.UserGivenName,
                        Surname = identity.UserSurname,
                        Email = identity.UserEmail,
                        IconPng = identity.UserIconPng
                    });
                }
            }

            return returnVal;
        }

        public async Task<List<ClientIdentity>> GetClientIdentities()
        {
            await InitializeIfNeeded().ConfigureAwait(false);
            List<ClientIdentity> returnVal = new List<ClientIdentity>();
            foreach (UserClientSecretInfo identity in _identities.Values)
            {
                if (!string.IsNullOrEmpty(identity.ClientId))
                {
                    returnVal.Add(new ClientIdentity()
                    {
                        AuthProvider = identity.AuthProvider,
                        Id = identity.ClientId,
                        Name = identity.ClientName,
                    });
                    break;
                }
            }

            return returnVal;
        }

        private async Task InitializeIfNeeded()
        {
            if (!_initialized)
            {
                await ReadFile().ConfigureAwait(false);
                _initialized = true;
            }
        }

        /// <summary>
        /// Writes the current list of identities to a file
        /// </summary>
        /// <returns></returns>
        private async Task WriteFile()
        {
            List<UserClientSecretInfo> flatValues = new List<UserClientSecretInfo>(_identities.Values);
            using (Stream fileStream = await _fileSystem.OpenStreamAsync(FILE_PATH, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
            {
                using (TextWriter textWriter = new StreamWriter(fileStream))
                {
                    using (JsonWriter jsonWriter = new JsonTextWriter(textWriter))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, flatValues);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the current list of identities from a file if it exists
        /// </summary>
        /// <returns></returns>
        private async Task ReadFile()
        {
            _identities.Clear();
            if (await _fileSystem.ExistsAsync(FILE_PATH).ConfigureAwait(false))
            {
                using (Stream fileStream = await _fileSystem.OpenStreamAsync(FILE_PATH, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                {
                    using (TextReader textReader = new StreamReader(fileStream))
                    {
                        using (JsonReader jsonReader = new JsonTextReader(textReader))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            List<UserClientSecretInfo> flatValues = serializer.Deserialize< List<UserClientSecretInfo>>(jsonReader);
                            foreach (UserClientSecretInfo secretInfo in flatValues)
                            {
                                ClientKeyIdentifier keyId = secretInfo.GetKeyId();
                                _identities[keyId] = secretInfo;
                            }
                        }
                    }
                }
            }
        }
    }
}
