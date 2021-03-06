﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using LessMarkup.Framework;
using LessMarkup.Interfaces.RecordModel;

namespace LessMarkup.MainModule.Model
{
    [RecordModel(TitleTextId = MainModuleTextIds.Import)]
    public class ImportLanguageModel
    {
        [InputField(InputFieldType.File, MainModuleTextIds.FileToImport, Required = true)]
        public InputFile ImportFile { get; set; }
    }
}
