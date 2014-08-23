﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Web;
using LessMarkup.Engine.Configuration;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Security;
using LessMarkup.Interfaces.System;
using LessMarkup.Interfaces.RecordModel;

namespace LessMarkup.UserInterface.Model.User
{
    [RecordModel(TitleTextId = UserInterfaceTextIds.Login)]
    public class LoginModel
    {
        private readonly ICurrentUser _currentUser;
        private readonly ISiteMapper _siteMapper;
        private readonly IDataCache _dataCache;
        private readonly IEngineConfiguration _engineConfiguration;

        [InputField(InputFieldType.Email, UserInterfaceTextIds.Email, Required = true)]
        public string Email { get; set; }

        [InputField(InputFieldType.Password, UserInterfaceTextIds.Password, Required = true)]
        public string Password { get; set; }

        public LoginModel(ICurrentUser currentUser, ISiteMapper siteMapper, IDataCache dataCache, IEngineConfiguration engineConfiguration)
        {
            _currentUser = currentUser;
            _siteMapper = siteMapper;
            _dataCache = dataCache;
            _engineConfiguration = engineConfiguration;
        }

        public object HandleStage1Request(Dictionary<string, object> data)
        {
            Thread.Sleep(new Random(Environment.TickCount).Next(30));
            var loginHash = _currentUser.LoginHash(data["user"].ToString());

            return new
            {
                Pass1 = loginHash.Item1,
                Pass2 = loginHash.Item2,
            };
        }

        public object HandleStage2Request(Dictionary<string, object> data)
        {
            var email = data["user"].ToString();
            var passwordHash = data["hash"].ToString();
            var savePassword = data["remember"].ToString();

            string administratorKey;

            object temp;

            if (!data.TryGetValue("administratorKey", out temp))
            {
                administratorKey = "";
            }
            else
            {
                administratorKey = temp != null ? temp.ToString() : null;
            }

            string adminLoginPage;

            if (_siteMapper.SiteId.HasValue)
            {
                var siteConfiguration = _dataCache.Get<ISiteConfiguration>();
                adminLoginPage = siteConfiguration.AdminLoginPage;
            }
            else
            {
                adminLoginPage = _engineConfiguration.AdminLoginPage;
            }

            var allowAdministrator = string.IsNullOrWhiteSpace(adminLoginPage) || administratorKey == adminLoginPage;

            var allowUser = string.IsNullOrWhiteSpace(adminLoginPage);

            if (!_currentUser.LoginWithPassword(email, "", savePassword != null && savePassword == true.ToString(), allowAdministrator, allowUser,
                HttpContext.Current.Request.UserHostAddress, passwordHash))
            {
                throw new UnauthorizedAccessException("User not found or wrong password");
            }

            return new
            {
                UserName = _currentUser.Email,
                ShowConfiguration = _currentUser.IsAdministrator,
                Path = string.IsNullOrWhiteSpace(adminLoginPage) ? "" : "/"
            };
        }

        public object HandleLogout()
        {
            _currentUser.Logout();
            return new {};
        }
    }
}
