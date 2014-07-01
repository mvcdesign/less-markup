﻿using LessMarkup.Interfaces.RecordModel;

namespace LessMarkup.UserInterface.Model.Common
{
    [RecordModel(TitleTextId = UserInterfaceTextIds.FlatPageSettings)]
    public class FlatPageSettingsModel
    {
        [InputField(InputFieldType.CheckBox, UserInterfaceTextIds.LoadOnShow, DefaultValue = false)]
        public bool LoadOnShow { get; set; }

    }
}
