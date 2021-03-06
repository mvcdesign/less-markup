﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using LessMarkup.DataFramework;
using LessMarkup.Engine.Scripting;
using LessMarkup.Framework;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.RecordModel;
using LessMarkup.Interfaces.System;
using Newtonsoft.Json;

namespace LessMarkup.Engine.Structure
{
    class RecordModelDefinition : IRecordModelDefinition
    {
        private readonly List<InputFieldDefinition> _fields = new List<InputFieldDefinition>();
        private readonly List<ColumnDefinition> _columns = new List<ColumnDefinition>();

        public Type CollectionType { get; set; }
        public object TitleTextId { get; set; }
        public string ModuleType { get; set; }
        public Type ModelType { get; set; }
        public Type DataType { get; set; }
        public string Id { get; set; }
        public bool SubmitWithCaptcha { get; set; }

        public IReadOnlyList<InputFieldDefinition> Fields { get { return _fields; } }

        public IReadOnlyList<ColumnDefinition> Columns { get { return _columns; } }

        private readonly IDataCache _dataCache;
        private readonly IEngineConfiguration _engineConfiguration;

        public RecordModelDefinition(IDataCache dataCache, IEngineConfiguration engineConfiguration)
        {
            _dataCache = dataCache;
            _engineConfiguration = engineConfiguration;
        }

        public void Initialize(Type type, RecordModelAttribute formType, string moduleType)
        {
            TitleTextId = formType.TitleTextId;
            ModuleType = moduleType;
            ModelType = type;
            DataType = formType.DataType;
            CollectionType = formType.CollectionType;
            CollectionType = formType.CollectionType;
            SubmitWithCaptcha = formType.SubmitWithCaptcha;
            var languageId = _dataCache.Get<ILanguageCache>().CurrentLanguageId;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldAttribute = property.GetCustomAttribute<InputFieldAttribute>();

                if (fieldAttribute != null)
                {
                    var fieldDefinition = new InputFieldDefinition(fieldAttribute, property);
                    _fields.Add(fieldDefinition);
                }

                var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();

                if (columnAttribute != null)
                {
                    var columnDefinition = DependencyResolver.Resolve<ColumnDefinition>();
                    columnDefinition.Initialize(columnAttribute, property);
                    _columns.Add(columnDefinition);

                    if (columnDefinition.CellTemplate != null && columnDefinition.CellTemplate.StartsWith("~/"))
                    {
                        columnDefinition.CellTemplate = _dataCache.Get<IResourceCache>(languageId).ReadText(columnDefinition.CellTemplate);
                    }
                }
            }
        }

        private const string ChallengeFieldKey = "-RecaptchaChallenge-";
        private const string ResponseFieldKey = "-RecaptchaResponse-";

        public void ValidateInput(object objectToValidate, bool isNew, string properties)
        {
            if (SubmitWithCaptcha)
            {
                if (string.IsNullOrWhiteSpace(properties))
                {
                    throw new Exception("Cannot validate captcha");
                }

                var propertiesDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(properties);

                var challengeValue = propertiesDictionary[ChallengeFieldKey].ToString();
                var responseValue = propertiesDictionary[ResponseFieldKey].ToString();

                var validator = new Recaptcha.RecaptchaValidator
                {
                    PrivateKey = _engineConfiguration.RecaptchaPrivateKey,
                    Challenge = challengeValue,
                    Response = responseValue,
                    RemoteIP = HttpContext.Current.Request.UserHostAddress
                };

                var response = validator.Validate();

                if (!response.IsValid)
                {
                    throw new Exception(response.ErrorMessage);
                }
            }

            foreach (var field in _fields)
            {
                if (!field.Required)
                {
                    continue;
                }

                if (field.Type == InputFieldType.File && !isNew)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(field.VisibleCondition))
                {
                    if (!ScriptHelper.EvaluateExpression(field.VisibleCondition, objectToValidate))
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(field.ReadOnlyCondition))
                {
                    if (ScriptHelper.EvaluateExpression(field.ReadOnlyCondition, objectToValidate))
                    {
                        continue;
                    }
                }

                var property = objectToValidate.GetType().GetProperty(field.Property.FromJsonCase(), BindingFlags.Public | BindingFlags.Instance);

                if (property != null)
                {
                    var propertyValue = property.GetValue(objectToValidate);

                    if (propertyValue != null)
                    {
                        var propertyText = propertyValue.ToString().Trim();
                        if (propertyText.Length > 0)
                        {
                            continue;
                        }
                    }
                }

                var errorText = LanguageHelper.GetText(Constants.ModuleType.UserInterface, MainModuleTextIds.PropertyMustBeSpecified);
                var fieldText = field.TextId == null ? "" : LanguageHelper.GetText(ModuleType, field.TextId);

                throw new Exception(string.Format(errorText, fieldText));
            }
        }
    }
}
