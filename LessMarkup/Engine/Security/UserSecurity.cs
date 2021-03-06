﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using LessMarkup.DataFramework;
using LessMarkup.DataObjects.Security;
using LessMarkup.Engine.Security.Models;
using LessMarkup.Framework;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.System;
using Newtonsoft.Json;

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

        public UserSecurity(IDomainModelProvider domainModelProvider, IEngineConfiguration engineConfiguration, IDataCache dataCache, IMailSender mailSender, IChangeTracker changeTracker)
        {
            _domainModelProvider = domainModelProvider;
            _engineConfiguration = engineConfiguration;
            _dataCache = dataCache;
            _mailSender = mailSender;
            _changeTracker = changeTracker;
        }

        #endregion

        #region IUserSecurity Implementation

        public string CreatePasswordChangeToken(long userId)
        {
            this.LogDebug("Creating password validation token for user " + userId.ToString(CultureInfo.InvariantCulture));

            using (var domainModel = _domainModelProvider.Create())
            {
                var user = domainModel.Query().From<User>().FindOrDefault<User>(userId);
                user.PasswordChangeToken = GenerateUniqueId();
                user.PasswordChangeTokenExpires = DateTime.UtcNow.AddMinutes(10);
                domainModel.Update(user);
                this.LogDebug("Created password validation token " + user.PasswordChangeToken);
                return user.PasswordChangeToken;
            }
        }

        public long? ValidatePasswordChangeToken(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            this.LogDebug("Validating password change token " + token);

            using (var domainModel = _domainModelProvider.Create())
            {
                var user = domainModel.Query().From<User>().Where("Email = $ AND PasswordChangeToken = $", email, token).FirstOrDefault<User>();
                if (user == null)
                {
                    this.LogDebug(string.Format("Cannot validate password - cannot find user for email '{0}' and token '{1}'", email, token));
                    return null;
                }
                if (!user.PasswordChangeTokenExpires.HasValue || user.PasswordChangeTokenExpires.Value < DateTime.UtcNow)
                {
                    this.LogDebug(string.Format("Password change for user '{0}' token error - date is expired", email));
                    return null;
                }

                this.LogDebug("Password validated ok");
                return user.Id;
            }
        }

        public void ChangePassword(string password, out string salt, out string encodedPassword)
        {
            salt = GenerateSalt();
            encodedPassword = EncodePassword(password, salt);
        }

        public long CreateUser(string username, string password, string email, string address, UrlHelper urlHelper, bool preApproved, bool generatePassword)
        {
            ValidateNewUserProperties(username, password, email, generatePassword);

            User user;

            using (var domainModel = _domainModelProvider.CreateWithTransaction())
            {
                CheckUserExistence(email, domainModel);

                user = CreateUserObject(username, email);

                if (generatePassword)
                {
                    if (!_engineConfiguration.SmtpConfigured)
                    {
                        throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.SmtpNotConfigured));
                    }

                    user.Password = GeneratePassword();
                    user.PasswordAutoGenerated = true;
                    user.RegistrationExpires = DateTime.UtcNow + TimeSpan.FromDays(1);
                }

                user.Password = EncodePassword(password, user.Salt);

                try
                {
                    domainModel.Create(user);
                }
                catch (SqlException e)
                {
                    if (e.Number == 2627 || e.Number == 2601 || e.Number == 2512)
                    {
                        this.LogException(e);
                        throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.NameOrEmailExists));
                    }

                    throw;
                }

                _changeTracker.AddChange<User>(user.Id, EntityChangeType.Added, domainModel);

                AddToDefaultGroup(domainModel, user);

                if (generatePassword)
                {
                    SendGeneratedPassword(email, password, user);
                }

                var siteConfiguration = _dataCache.Get<ISiteConfiguration>();

                if (preApproved)
                {
                    // means the user is created manually by the administrator
                    user.EmailConfirmed = true;
                    user.IsApproved = true;
                    domainModel.Update(user);
                    UserNotifyCreated(user, password);
                }
                else
                {
                    user.ValidateSecret = Guid.NewGuid().ToString().Replace("-", "");
                    domainModel.Update(user);

                    if (!siteConfiguration.AdminApproveNewUsers)
                    {
                        user.IsApproved = true;
                    }

                    SendConfirmationLink(urlHelper, user);
                }

                if (siteConfiguration.AdminNotifyNewUsers)
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
            if (obj == null)
            {
                return null;
            }

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Formatting.None));
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

            return Convert.ToBase64String(data).Replace('+', '_');
        }

        public T DecryptObject<T>(string encrypted) where T : class
        {
            if (string.IsNullOrEmpty(encrypted))
            {
                return null;
            }
            var decrypted = Convert.FromBase64String(encrypted.Replace('_', '+'));
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

            try
            {
                return
                    JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(decrypted, SaltLength,
                        decrypted.Length - HashSize - SaltLength));
            }
            catch (Exception e)
            {
                this.LogException(e);
                return null;
            }
        }

        public void ValidateInputFile(InputFile file)
        {
            if (file == null)
            {
                return;
            }

            var configuration = _dataCache.Get<ISiteConfiguration>();

            if (file.File.Length > configuration.MaximumFileSize)
            {
                throw new Exception(string.Format(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.MaximumFileSizeReached), configuration.MaximumFileSize));
            }

            var found = false;

            var sourceFileType = file.Type.ToLower().Trim();

            foreach (var fileType in configuration.ValidFileType.Split(new[] {','}).Select(s => s.ToLower().Trim()))
            {
                if (fileType.EndsWith("*"))
                {
                    if (sourceFileType.StartsWith(fileType.Substring(0, fileType.Length-1)))
                    {
                        found = true;
                        break;
                    }
                    continue;
                }

                if (fileType == sourceFileType)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.UnsupportedFileType));
            }

            var sourceExtension = Path.GetExtension(file.Name);

            if (sourceExtension == null)
            {
                return;
            }
            
            sourceExtension = sourceExtension.TrimStart(new []{'.'}).Trim().ToLower();

            found = false;

            foreach (var extension in configuration.ValidFileExtension.Split(new[] { ',' }).Select(s => s.ToLower().Trim()))
            {
                if (extension == sourceExtension)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.UnsupportedFileType));
            }
        }

        [Serializable]
        public class AccessToken
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
                var user = domainModel.Query().From<User>().Where("ValidateSecret = $ AND EmailConfirmed = $", validateSecret, false).FirstOrDefault<User>();
                if (user == null)
                {
                    userId = 0;
                    return false;
                }

                user.ValidateSecret = null;
                user.EmailConfirmed = true;

                if (!_dataCache.Get<ISiteConfiguration>().AdminApproveNewUsers)
                {
                    user.IsApproved = true;
                }

                _changeTracker.AddChange<User>(user.Id, EntityChangeType.Updated, domainModel);
                domainModel.Update(user);
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
                EmailConfirmed = false,
                LastLogin = DateTime.UtcNow,
                LastBlock = null,
                LastActivity = DateTime.UtcNow
            };

            return user;
        }

        private static void ValidateNewUserProperties(string username, string password, string email, bool generatePassword)
        {
            if (!TextValidator.CheckUsername(username))
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.InvalidUserName));
            }

            if (!generatePassword && !TextValidator.CheckPassword(password))
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.InvalidPassword));
            }

            if (!generatePassword && !TextValidator.CheckNewPassword(password))
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.PasswordTooSimple));
            }

            while (!string.IsNullOrEmpty(email) && (email[email.Length - 1] == '.' || Char.IsWhiteSpace(email[email.Length - 1])))
            {
                email = email.Remove(email.Length - 1, 1);
            }

            if (!TextValidator.CheckTextField(email) || !EmailCheck.IsValidEmail(email))
            {
                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.InvalidEmail, email));
            }
        }

        private void CheckUserExistence(string email, IDomainModel domainModel)
        {
            var user = domainModel.Query().From<User>().Where("Email = $ AND IsRemoved = $", email, false).FirstOrDefault<User>("Id");

            if (user != null)
            {
                this.LogDebug(string.Format("User with e-mail '{0}' already exists", email));

                throw new Exception(LanguageHelper.GetText(Constants.ModuleType.MainModule, MainModuleTextIds.InvalidEmail, email));
            }
        }

        private void AddToDefaultGroup(IDomainModel domainModel, User user)
        {
            var defaultGroup = _dataCache.Get<ISiteConfiguration>().DefaultUserGroup;

            if (!string.IsNullOrEmpty(defaultGroup))
            {
                var group = domainModel.Query().From<UserGroup>().Where("Name = $", defaultGroup).FirstOrDefault<UserGroup>();

                if (group == null)
                {
                    group = new UserGroup {Name = defaultGroup};
                    domainModel.Create(group);
                }

                var membership = new UserGroupMembership {UserId = user.Id, UserGroupId = @group.Id};
                domainModel.Create(membership);
            }
        }

        private void SendConfirmationLink(UrlHelper urlHelper, User user)
        {
            var confirmationLink = urlHelper.Action("Validate", "Account", new {secret = user.ValidateSecret});

            var uri = HttpContext.Current.Request.Url;

            confirmationLink = string.Format("{0}://{1}:{2}{3}", uri.Scheme, uri.Host, uri.Port, confirmationLink);

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

            var administrators = domainModel.Query().From<User>().Where("IsAdministrator = $ AND IsRemoved = $ AND IsBlocked = $", true, false, false).ToList<User>("Id");
            foreach (var admin in administrators)
            {
                _mailSender.SendMail(null, admin.Id, null, Constants.MailTemplates.AdminNewUserCreated, model);
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
