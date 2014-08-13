﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Reflection;
using LessMarkup.DataFramework;
using LessMarkup.Engine.HtmlTemplate;
using LessMarkup.Engine.Language;
using LessMarkup.Engine.Scripting;
using LessMarkup.Framework.Helpers;
using LessMarkup.Interfaces;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.RecordModel;

namespace LessMarkup.Engine.Structure
{
    class RecordModelDefinition : IRecordModelDefinition
    {
        private readonly List<InputFieldDefinition> _fields = new List<InputFieldDefinition>();
        private readonly List<ColumnDefinition> _columns = new List<ColumnDefinition>();

        public Type CollectionType { get; set; }
        public object TitleTextId { get; set; }
        public string ModuleType { get; set; }
        public Type DataType { get; set; }
        public string Id { get; set; }

        public IReadOnlyList<InputFieldDefinition> Fields { get { return _fields; } }

        public IReadOnlyList<ColumnDefinition> Columns { get { return _columns; } }

        private readonly IDataCache _dataCache;

        public RecordModelDefinition(IDataCache dataCache)
        {
            _dataCache = dataCache;
        }

        public void Initialize(Type type, RecordModelAttribute formType, string moduleType)
        {
            TitleTextId = formType.TitleTextId;
            ModuleType = moduleType;
            DataType = type;
            CollectionType = formType.CollectionType;
            CollectionType = formType.CollectionType;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldAttribute = property.GetCustomAttribute<InputFieldAttribute>();

                if (fieldAttribute != null)
                {
                    var fieldDefinition = DependencyResolver.Resolve<InputFieldDefinition>();
                    fieldDefinition.Initialize(fieldAttribute, property, moduleType);
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
                        columnDefinition.CellTemplate = _dataCache.Get<HtmlTemplateCache>().GetTemplate(columnDefinition.CellTemplate);
                    }
                }
            }
        }

        public void ValidateInput(object objectToValidate, bool isNew)
        {
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

                var property = objectToValidate.GetType().GetProperty(field.Property, BindingFlags.Public | BindingFlags.Instance);

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
                var fieldText = LanguageHelper.GetText(ModuleType, field.TextId);

                throw new Exception(string.Format(errorText, fieldText));
            }
        }
    }
}
