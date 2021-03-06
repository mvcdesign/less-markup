﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

namespace LessMarkup.DataFramework
{
    public static class Constants
    {
        public static class ModuleType
        {
            public const string None = null;
            public const string MainModule = "Main";
            public const string UserInterface = "UserInterface";
        }

        public static class Minify
        {
            public const string JsMinify = "minify.js";
            public const string CssMinify = "minify.css";
        }

        public static class DataAccessGenerator
        {
            public const string DefaultNamespace = "LessMarkup.DataAccessGen";
            public const string DomainModelClassName = "DomainModel";

            public static readonly string[] UsingNamespaces =
            {
                "LessMarkup.DataFramework.DataAccess",
                "System.Data.Entity.Migrations",
                "System.Data.Entity",
                "System"
            };
        }

        public static class MailTemplates
        {
            public const string ValidateUser = "~/Views/EmailTemplates/ConfirmUserRegistration.html";
            public const string PasswordGeneratedNotification = "~/Views/EmailTemplates/PasswordGeneratedNotification.html";
            public const string AdminNewUserCreated = "~/Views/EmailTemplates/AdminNewUserCreated.html";
            public const string UserNewUserCreated = "~/Views/EmailTemplates/UserNewUserCreated.html";
            public const string ConfirmUserRegistration = "~/Views/EmailTemplates/ConfirmUserRegistration.html";
            public const string SendContact = "~/Views/EmailTemplates/SendContact.html";
            public const string ApproveUserRegistration = "~/Views/EmailTemplates/ApproveUserRegistration.html";
            public const string ConfirmUserApproval = "~/Views/EmailTemplates/ConfirmUserApproval.html";
        }

        public static class RequestItemKeys
        {
            public const string ContextData = "ContextData";
// ReSharper disable MemberHidesStaticFromOuterClass
            public const string ContextMenu = "ContextMenu";
// ReSharper restore MemberHidesStaticFromOuterClass
            public const string SitePath = "SitePath";
            public const string Title = "Title";
            public const string Toolbar = "Toolbar";
            public const string InsertionPoint = "InsertionPoint";
            public const string ErrorFlag = "ErrorFlag";
            public const string ResponseFilter = "ResponseFilter";
        }

        public static class Menu
        {
            public static class Positions
            {
                public const string Top = "top";
                public const string Left = "left";
                public const string Right = "right";
            }
        }

        public static class Configuration
        {
            public const string Common = "Common";
            public const string ConfigurationFlag = "IsConfiguration";
        }

        public static class DomainModel
        {
            public const string ConnectionName = "Server";
        }

        public static class EventLog
        {
            public const string Source = "Application";
        }

        public static class Routes
        {
            public const string DefaultRoute = "Default";
            public const string GlobalRoute = "Global";
            public const string DefaultController = "Home";
            public const string DefaultAction = "Index";
            public const string GlobalController = "Global";
            public const string StartupController = "Startup";
        }

        public static class ContextMenu
        {
            public const string UserProfile = "UserProfile";
            public const string TopLinks = "TopLinks";
            public const string Administrative = "Administrative";
            public const string LeftMenu = "LeftMenu";
        }

        public static class MailMessage
        {
            public const int MaximumSize = 1024*1024*8;
        }

        public static class Engine
        {
            public const int CheckConfigurationChangeMinutes = 2;
            public const string NoScriptBlock = "<noscript><iframe style=\"width:100%;border:none;\" src=\"?noscript\"></iframe></noscript>";
        }

        public static class NodePath
        {
            public const string Configuration = "configuration";
            public const string Profile = "profile";
            public const string UserCards = "users";
            public const string ForgotPassword = "forgot_password";
        }
    }
}
