﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.ComponentModel.DataAnnotations.Schema;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Text;

namespace LessMarkup.Forum.DataObjects
{
    public class PostAttachment : SiteDataObject
    {
        [ForeignKey("Post")]
        public long PostId { get; set; }
        public Post Post { get; set; }
        [TextSearch]
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }
    }
}
