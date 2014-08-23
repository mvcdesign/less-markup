﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using LessMarkup.DataFramework;
using LessMarkup.DataFramework.DataAccess;
using LessMarkup.DataObjects.Security;
using LessMarkup.Engine.Configuration;
using LessMarkup.Engine.Security.Models;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.System;

namespace LessMarkup.Engine.Security
{
    class UserSecurity : IUserSecurity
    {
        #region Private Fields

        private const int SaltLength = 16;
        // ReSharper disable once InconsistentNaming
        private static readonly int HashSize;
        private readonly IDomainModelProvider _domainModelProvider;
        private readonly IEngineConfiguration _engineConfiguration;
        private readonly IDataCache _dataCache;
        private readonly IMailSender _mailSender;
        private readonly ISiteMapper _siteMapper;
        private readonly IChangeTracker _changeTracker;
        private const string HexCodes = "0123456789abcdef";

        #endregion

        #region Initialization

        static UserSecurity()
        {
            using (var hash = CreateHashAlgorithm())
            {
                HashSize = hash.HashSize / 8;
            }
        }

        public UserSecurity(IDomainModelProvider domainModelProvider, IEngineConfiguration engineConfiguration, IDataCache dataCache, IMailSender mailSender, ISiteMapper siteMapper, IChangeTracker changeTracker)
        {
            _domainModelProvider = domainModelProvider;
            _engineConfiguration = engineConfiguration;
            _dataCache = dataCache;
            _mailSender = mailSender;
            _siteMapper = siteMapper;
            _changeTracker = changeTracker;
        }

        #endregion

        #region IUserSecurity Implementation

        public string CreatePasswordValidationToken(long? userId)
        {
            return CreateAccessToken(AbstractDomainModel.GetCollectionId<User>(), 0, EntityAccessType.Everyone, userId, DateTime.UtcNow + TimeSpan.FromMinutes(10));
        }

        public void ChangePassword(string password, out string salt, out string encodedPassword)
        {
            salt = GenerateSalt();
            encodedPassword = EncodePassword(password, salt);
        }

        public long CreateUser(string username, string password, string email, string address, UrlHelper urlHelper, bool preApproved, bool generatePassword)
        {
            if (!_siteMapper.SiteId.HasValue && !preApproved)
            {
                throw new Exception("Cannot create user for global site");
            }

            ValidateNewUserProperties(username, password, email, generatePassword);

            User user;

            using (var domainModel = _domainModelProvider.CreateWithTransaction())
            {
                CheckUserExistence(email, domainModel);

                user = CreateUserObject(username, email);

                if (generatePassword)
                {
                    password = GenerateUserPassword(user);
                    user.PasswordAutoGenerated = true;
                }

                user.Password = EncodePassword(password, user.Salt);

                try
                {
                    domainModel.GetCollection<User>().Add(user);
                    domainModel.SaveChanges();
                }
                catch (SqlException e)
                {
                    if (e.Number == 2627 || e.Number == 2601 || e.Number == 2512)
                    {
                        throw new Exception("User with specified name or e-mail already exists");
                    }

                    throw;
                }

                _changeTracker.AddChange<User>(user.Id, EntityChangeType.Added, domainModel);

                if (_siteMapper.SiteId.HasValue)
                {
                    AddToDefaultGroup(domainModel, user);
                }

                domainModel.SaveChanges();

                if (generatePassword)
                {
                    SendGeneratedPassword(email, password, user);
                }

                var siteConfiguration = _dataCache.Get<ISiteConfiguration>();

                if (preApproved)
                {
                    // means the user is created manually by the administrator
                    user.IsValidated = true;
                    user.IsApproved = true;
                    domainModel.SaveChanges();
                    UserNotifyCreated(user, password);
                }
                else
                {
                    if (!siteConfiguration.AdminApproveNewUsers)
                    {
                        user.IsApproved = true;
                    }
                    SendConfirmationLink(urlHelper, user);
                }

                if (_siteMapper.SiteId.HasValue && siteConfiguration.AdminNotifyNewUsers)
                {
                    AdminNotifyNewUsers(user, domainModel);
                }

                domainModel.CompleteTransaction();
            }

            return user.Id;
        }

        private static byte[] GetSaltBytes()
        {
            using (var cryptoProvider = new RNGCryptoServiceProvider())
            {
                var salt = new byte[SaltLength];
                cryptoProvider.GetBytes(salt);
                return salt;
            }
        }

        private static HashAlgorithm CreateHashAlgorithm()
        {
            return HashAlgorithm.Create("SHA512");
        }

        private static byte[] CreateHash(byte[] data, int offset = 0, int? count = null)
        {
            using (var hashAlgorithm = CreateHashAlgorithm())
            {
                if (hashAlgorithm == null)
                {
                    return null;
                }
                return hashAlgorithm.ComputeHash(data, offset, count ?? data.Length);
            }
        }

        public string EncryptObject(object obj)
        {
            var formatter = new BinaryFormatter();
            byte[] bytes;
            using (var memoryStream = new MemoryStream())
            {
                formatter.Serialize(memoryStream, obj);
                bytes = memoryStream.ToArray();
            }
            var salt = GetSaltBytes();
            var hash = CreateHash(bytes);

            if (hash == null)
            {
                return null;
            }

            var data = new byte[bytes.Length + salt.Length + hash.Length];

            Buffer.BlockCopy(salt, 0, data, 0, salt.Length);
            Buffer.BlockCopy(bytes, 0, data, salt.Length, bytes.Length);
            Buffer.BlockCopy(hash, 0, data, salt.Length+bytes.Length, hash.Length);

            data = MachineKey.Protect(data, null);

            return Convert.ToBase64String(data);
        }

        public T DecryptObject<T>(string encrypted) where T : class
        {
            var decrypted = Convert.FromBase64String(encrypted);
            decrypted = MachineKey.Unprotect(decrypted, null);
            if (decrypted == null)
            {
                return null;
            }

            var hash = CreateHash(decrypted, SaltLength, decrypted.Length-HashSize-SaltLength);

            var hashOffset = decrypted.Length - HashSize;

            for (int i = 0; i < HashSize; i++)
            {
                if (hash[i] != decrypted[hashOffset + i])
                {
                    return null;
                }
            }

            using (var memoryStream = new MemoryStream(decrypted, SaltLength, decrypted.Length - HashSize - SaltLength))
            {
                var formatter = new BinaryFormatter();
                return (T) formatter.Deserialize(memoryStream);
            }
        }

        class AccessToken
        {
            public long? UserId { get; set; }
            public int CollectionId { get; set; }
            public long EntityId { get; set; }
            public EntityAccessType AccessType { get; set; }
            public long? Ticks { get; set; }
        }

        public string CreateAccessToken(int collectionId, long entityId, EntityAccessType accessType, long? userId, DateTime? expirationTime = null)
        {
            if (accessType == EntityAccessType.Everyone)
            {
                userId = null;
            }

            return EncryptObject(new AccessToken
            {
                UserId = userId,
                CollectionId = collectionId,
                EntityId = entityId,
                AccessType = accessType,
                Ticks = expirationTime != null ? expirationTime.Value.Ticks : (long?)null
            });
        }

        public bool ValidateAccessToken(string token, int collectionId, long entityId, EntityAccessType accessType, long? userId)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var accessToken = DecryptObject<AccessToken>(token);

            if (accessToken == null)
            {
                return false;
            }

            switch (accessToken.AccessType)
            {
                case EntityAccessType.Read:
                    if ((accessType != EntityAccessType.Read && accessType != EntityAccessType.Everyone) || !userId.HasValue || accessToken.UserId != userId.Value)
                    {
                        return false;
                    }
                    break;
                case EntityAccessType.ReadWrite:
                    if ((accessType != EntityAccessType.Read && accessType != EntityAccessType.ReadWrite && accessType != EntityAccessType.Everyone) || !userId.HasValue || accessToken.UserId != userId.Value)
                    {
                        return false;
                    }
                    break;
                case EntityAccessType.Everyone:
                    break;
                default:
                    return false;
            }

            if (accessToken.CollectionId != collectionId)
            {
                return false;
            }
            if (accessToken.EntityId != entityId)
            {
                return false;
            }

            if (accessToken.Ticks.HasValue)
            {
                if (DateTime.UtcNow.Ticks > accessToken.Ticks.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public string GenerateUniqueId()
        {
            var currentTime = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            var data = GetSaltBytes();
            var ret = new StringBuilder();
            foreach (var b in data)
            {
                AppendHex(b, ret);
            }
            foreach (var b in currentTime)
            {
                AppendHex(b, ret);
            }
            return ret.ToString();
        }

        public bool ConfirmUser(string validateSecret, out long userId)
        {
            using (var domainModel = _domainModelProvider.CreateWithTransaction())
            {
                var user = domainModel.GetCollection<User>().FirstOrDefault(u => u.ValidateSecret == validateSecret && !u.IsValidated && u.SiteId.HasValue && u.SiteId.Value == _siteMapper.SiteId);
                if (user == null)
                {
                    userId = 0;
                    return false;
                }
                user.ValidateSecret = null;
                user.IsValidated = true;

                if (!_dataCache.Get<ISiteConfiguration>().AdminApproveNewUsers)
                {
                    user.IsApproved = true;
                }

                _changeTracker.AddChange<User>(user.Id, EntityChangeType.Updated, domainModel);
                domainModel.SaveChanges();
                domainModel.CompleteTransaction();
                userId = user.Id;
                return true;
            }
        }

        #endregion

        #region Helper Methods

        public static string EncodePassword(string password, string salt)
        {
            using (var hashAlgorithm = CreateHashAlgorithm())
            {
                if (hashAlgorithm == null)
                {
                    throw new NotSupportedException();
                }
                var bytes = Encoding.UTF8.GetBytes(salt + password);
                return ToHexString(hashAlgorithm.ComputeHash(bytes));
            }
        }

        public static void AppendHex(byte b, StringBuilder builder)
        {
            builder.Append(HexCodes[b >> 4]);
            builder.Append(HexCodes[b & 0xF]);
        }

        public static string ToHexString(byte[] values)
        {
            var sb = new StringBuilder();
            foreach (var b in values)
            {
                AppendHex(b, sb);
            }
            return sb.ToString();
        }

        public static string ToHexString(byte[] values, int start, int count)
        {
            var sb = new StringBuilder();
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                AppendHex(values[i], sb);
            }
            return sb.ToString();
        }

        private const string PasswordDictionary = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_-!?";

        public string GeneratePassword(int passwordLength = 8)
        {
            var data = GetSaltBytes();

            string ret = "";

            for (int i = 0; i < passwordLength; i++)
            {
                ret += PasswordDictionary[data[i]%PasswordDictionary.Length];
            }

            return ret;
        }

        public static string GenerateSalt()
        {
            var data = new byte[16];
            new RNGCryptoServiceProvider().GetBytes(data);
            return Convert.ToBase64String(data);
        }

        private User CreateUserObject(string username, string email)
        {
            var user = new User
            {
                Salt = GenerateSalt(),
                Email = email,
                Name = username,
                Registered = DateTime.UtcNow,
                IsBlocked = false,
                IsValidated = false,
                LastLogin = DateTime.UtcNow,
                LastBlock = null,
                LastActivity = DateTime.UtcNow,
                ValidateSecret = Guid.NewGuid().ToString(),
                SiteId = _siteMapper.SiteId
            };

            return user;
        }

        private static void ValidateNewUserProperties(string username, string password, string email, bool generatePassword)
        {
            if (!TextValidator.CheckUsername(username))
            {
                throw new Exception("Invalid User Name");
            }

            if (!generatePassword && !TextValidator.CheckPassword(password))
            {
                throw new Exception("Invalid Password");
            }

            if (!generatePassword && !TextValidator.CheckNewPassword(password))
            {
                throw new Exception("Password does not meet minimal complexity criteria");
            }

            while (!string.IsNullOrEmpty(email) && (email[email.Length - 1] == '.' || Char.IsWhiteSpace(email[email.Length - 1])))
            {
                email = email.Remove(email.Length - 1, 1);
            }

            if (!TextValidator.CheckTextField(email) || !EmailCheck.IsValidEmail(email))
            {
                throw new Exception("Invalid E-Mail");
            }
        }

        private string GenerateUserPassword(User user)
        {
            if (!_engineConfiguration.SmtpConfigured)
            {
                throw new Exception("SMTP is not configured");
            }

            var password = GeneratePassword();

            user.IsValidated = true;
            user.LastActivity = null;
            user.LastLogin = null;
            user.LastPasswordChanged = null;
            user.ValidateSecret = null;
            user.RegistrationExpires = DateTime.UtcNow + TimeSpan.FromDays(1);
            return password;
        }

        private void CheckUserExistence(string email, IDomainModel domainModel)
        {
            if (domainModel.GetCollection<User>().Any(c => c.Email == email && !c.IsRemoved && c.SiteId == _siteMapper.SiteId))
            {
                throw new Exception("User with specified e-mail already exists");
            }
        }

        private void AddToDefaultGroup(IDomainModel domainModel, User user)
        {
            var defaultGroup = _dataCache.Get<ISiteConfiguration>().DefaultUserGroup;

            if (!string.IsNullOrEmpty(defaultGroup))
            {
                var group = domainModel.GetSiteCollection<UserGroup>().SingleOrDefault(g => g.Name == defaultGroup);

                if (group == null)
                {
                    group = domainModel.GetSiteCollection<UserGroup>().Create();
                    group.Name = defaultGroup;
                    domainModel.GetSiteCollection<UserGroup>().Add(group);
                    domainModel.SaveChanges();
                }

                var membership = domainModel.GetSiteCollection<UserGroupMembership>().Create();
                membership.UserId = user.Id;
                membership.UserGroupId = group.Id;
                domainModel.GetSiteCollection<UserGroupMembership>().Add(membership);
            }
        }

        private void SendConfirmationLink(UrlHelper urlHelper, User user)
        {
            var confirmationLink = urlHelper.Action("Validate", "Account", new {secret = user.ValidateSecret});
            var confirmationModel = new UserConfirmationMailTemplateModel { Link = confirmationLink };

            _mailSender.SendMail(null, user.Id, null, Constants.MailTemplates.ValidateUser, confirmationModel);
        }

        private void SendGeneratedPassword(string email, string password, User user)
        {
            var notificationModel = new GeneratedPassswordModel
            {
                Login = email,
                Password = password,
                SiteLink = HttpContext.Current.Request.Url.Host,
                SiteName = _dataCache.Get<ISiteConfiguration>().SiteName
            };

            _mailSender.SendMail(null, user.Id, null,
                Constants.MailTemplates.PasswordGeneratedNotification, notificationModel);
        }

        private void AdminNotifyNewUsers(User user, IDomainModel domainModel)
        {
            var model = new NewUserCreatedModel
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email
            };

            var administrators = domainModel.GetCollection<User>().Where(u => u.IsAdministrator && u.SiteId == _siteMapper.SiteId && !u.IsRemoved && !u.IsBlocked);
            foreach (var admin in administrators.Select(a => a.Id))
            {
                _mailSender.SendMail(null, admin, null, Constants.MailTemplates.AdminNewUserCreated, model);
            }
        }

        private void UserNotifyCreated(User user, string password)
        {
            var model = new NewUserCreatedModel
            {
                Email = user.Email,
                Password = password,
                SiteName = _dataCache.Get<ISiteConfiguration>().SiteName
            };

            _mailSender.SendMail(null, user.Id, null, Constants.MailTemplates.UserNewUserCreated, model);
        }

        #endregion
    }
}
