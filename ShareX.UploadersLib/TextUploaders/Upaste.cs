#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ShareX.UploadersLib.TextUploaders
{
    public class UpasteTextUploaderService : TextUploaderService
    {
        public override TextDestination EnumValue { get; } = TextDestination.Upaste;

        public override bool CheckConfig(UploadersConfig config) => true;

        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            return new Upaste(config.UpasteUserKey)
            {
                IsPublic = config.UpasteIsPublic
            };
        }
    }

    public sealed class Upaste : TextUploader
    {
        private const string APIURL = "https://upaste.me/api/v2/paste";

        public string UserKey { get; private set; }
        public bool IsPublic { get; set; }

        public Upaste(string userKey)
        {
            UserKey = userKey;
        }

        protected override async Task<UploadResult> UploadTextCoreAsync(string text, string fileName, CancellationToken cancellationToken)
        {
            UploadResult ur = new UploadResult();

            if (!string.IsNullOrEmpty(text))
            {
                NameValueCollection headers = new NameValueCollection();
                if (!string.IsNullOrEmpty(UserKey))
                {
                    headers.Add("Authorization", "Bearer " + UserKey);
                }

                Dictionary<string, string> arguments = new Dictionary<string, string>();
                arguments.Add("paste", text);
                //arguments.Add("syntax", "");
                //arguments.Add("name", "");
                arguments.Add("privacy", IsPublic ? "0" : "1"); // 0 public 1 unlisted
                arguments.Add("expire", "0");

                ur.Response = await SendRequestMultiPartAsync(APIURL, arguments, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(ur.Response))
                {
                    UpasteResponse response = JsonConvert.DeserializeObject<UpasteResponse>(ur.Response);

                    if (response != null)
                    {
                        if (response.status.Equals("success", StringComparison.OrdinalIgnoreCase))
                        {
                            ur.URL = response.paste.link;
                        }
                        else
                        {
                            Errors.Add(response.error);
                        }
                    }
                }
            }

            return ur;
        }

        public class UpastePaste
        {
            public string key { get; set; }
            public string link { get; set; }
            public string raw { get; set; }
            public string download { get; set; }
        }

        public class UpasteResponse
        {
            public UpastePaste paste { get; set; }
            public int errorcode { get; set; }
            public string error { get; set; }
            public string status { get; set; }
        }
    }
}
