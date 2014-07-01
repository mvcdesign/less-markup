﻿using LessMarkup.Framework.Language;
using LessMarkup.Interfaces.RecordModel;

namespace LessMarkup.MainModule.Model
{
    [RecordModel(TitleTextId = MainModuleTextIds.EditContactForm)]
    public class ContactFormModel
    {
        [InputField(InputFieldType.Email, MainModuleTextIds.ContactEmail, Required = true)]
        public string ContactEmail { get; set; }

        [InputField(InputFieldType.Text, MainModuleTextIds.ContactSubject, Required = true)]
        public string ContactSubject { get; set; }
    }
}
