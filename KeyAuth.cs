using System;
using System.Security.Cryptography;
using System.Collections.Specialized;
using System.Text;
using System.Net;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Diagnostics;
using System.Security.Principal;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms; // Cambiado para usar los cuadros de diálogo de Windows Forms
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;

namespace KeyAuth
{
    public class api
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort GlobalAddAtom(string lpString);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort GlobalFindAtom(string lpString);

        public string name, ownerid, version, path, seed;

        public api(string name, string ownerid, string version, string path = null)
        {
            if (ownerid.Length != 10)
            {
                error("Application not setup correctly. OwnerID must be 10 characters.");
                TerminateProcess(GetCurrentProcess(), 1);
            }

            this.name = name;
            this.ownerid = ownerid;
            this.version = version;
            this.path = path;
        }

        #region structures
        [DataContract]
        private class response_structure
        {
            [DataMember] public bool success { get; set; }
            [DataMember] public bool newSession { get; set; }
            [DataMember] public string sessionid { get; set; }
            [DataMember] public string contents { get; set; }
            [DataMember] public string response { get; set; }
            [DataMember] public string message { get; set; }
            [DataMember] public string ownerid { get; set; }
            [DataMember] public string download { get; set; }
            [DataMember(IsRequired = false, EmitDefaultValue = false)] public user_data_structure info { get; set; }
            [DataMember(IsRequired = false, EmitDefaultValue = false)] public app_data_structure appinfo { get; set; }
            [DataMember] public List<msg> messages { get; set; }
            [DataMember] public List<users> users { get; set; }
            [DataMember(Name = "2fa", IsRequired = false, EmitDefaultValue = false)] public TwoFactorData twoFactor { get; set; }
        }

        public class msg
        {
            public string message { get; set; }
            public string author { get; set; }
            public string timestamp { get; set; }
        }

        public class users
        {
            public string credential { get; set; }
        }

        [DataContract]
        private class user_data_structure
        {
            [DataMember] public string username { get; set; }
            [DataMember] public string ip { get; set; }
            [DataMember] public string hwid { get; set; }
            [DataMember] public string createdate { get; set; }
            [DataMember] public string lastlogin { get; set; }
            [DataMember] public List<Data> subscriptions { get; set; }
        }

        [DataContract]
        private class app_data_structure
        {
            [DataMember] public string numUsers { get; set; }
            [DataMember] public string numOnlineUsers { get; set; }
            [DataMember] public string numKeys { get; set; }
            [DataMember] public string version { get; set; }
            [DataMember] public string customerPanelLink { get; set; }
            [DataMember] public string downloadLink { get; set; }
        }
        #endregion

        private static string sessionid, enckey;
        bool initialized;

        public async Task init()
        {
            Random random = new Random();
            int length = random.Next(5, 51);
            StringBuilder sb = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                char randomChar = (char)random.Next(32, 127);
                sb.Append(randomChar);
            }

            seed = sb.ToString();
            checkAtom();

            var values_to_upload = new NameValueCollection
            {
                ["type"] = "init",
                ["ver"] = version,
                ["hash"] = checksum(Process.GetCurrentProcess().MainModule.FileName),
                ["name"] = name,
                ["ownerid"] = ownerid
            };

            if (!string.IsNullOrEmpty(path))
            {
                values_to_upload.Add("token", File.ReadAllText(path));
                values_to_upload.Add("thash", TokenHash(path));
            }

            var response = await req(values_to_upload);

            if (response == "KeyAuth_Invalid")
            {
                error("Application not found");
                TerminateProcess(GetCurrentProcess(), 1);
            }

            var json = response_decoder.string_to_generic<response_structure>(response);
            if (json.ownerid == ownerid)
            {
                load_response_struct(json);
                if (json.success)
                {
                    sessionid = json.sessionid;
                    initialized = true;
                }
                else if (json.message == "invalidver")
                {
                    app_data.downloadLink = json.download;
                }
            }
            else
            {
                TerminateProcess(GetCurrentProcess(), 1);
            }
        }

        private System.Threading.Timer atomTimer;
        void checkAtom()
        {
            atomTimer = new System.Threading.Timer(_ =>
            {
                ushort foundAtom = GlobalFindAtom(seed);
                if (foundAtom == 0)
                {
                    TerminateProcess(GetCurrentProcess(), 1);
                }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public static string TokenHash(string tokenPath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var s = File.OpenRead(tokenPath))
                {
                    byte[] bytes = sha256.ComputeHash(s);
                    return BitConverter.ToString(bytes).Replace("-", string.Empty);
                }
            }
        }

        public void CheckInit()
        {
            if (!initialized)
            {
                error("You must run the function KeyAuthApp.init(); first");
                TerminateProcess(GetCurrentProcess(), 1);
            }
        }

        public string expirydaysleft()
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            dtDateTime = dtDateTime.AddSeconds(long.Parse(user_data.subscriptions[0].expiry)).ToLocalTime();
            TimeSpan difference = dtDateTime - DateTime.Now;
            return Convert.ToString(difference.Days + " Days " + difference.Hours + " Hours Left");
        }

        public static DateTime UnixTimeToDateTime(long unixtime)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Local);
            try { dtDateTime = dtDateTime.AddSeconds(unixtime).ToLocalTime(); }
            catch { dtDateTime = DateTime.MaxValue; }
            return dtDateTime;
        }

        public async Task license(string key, string code = null)
        {
            CheckInit();
            string hwid = WindowsIdentity.GetCurrent().User.Value;

            var values_to_upload = new NameValueCollection
            {
                ["type"] = "license",
                ["key"] = key,
                ["hwid"] = hwid,
                ["sessionid"] = sessionid,
                ["name"] = name,
                ["ownerid"] = ownerid,
                ["code"] = code ?? null
            };

            var response = await req(values_to_upload);
            var json = response_decoder.string_to_generic<response_structure>(response);

            if (json.ownerid == ownerid)
            {
                GlobalAddAtom(seed);
                GlobalAddAtom(ownerid);
                load_response_struct(json);
                if (json.success)
                    load_user_data(json.info);
            }
            else
            {
                TerminateProcess(GetCurrentProcess(), 1);
            }
        }

        public static string checksum(string filename)
        {
            string result;
            using (MD5 md = MD5.Create())
            {
                using (FileStream fileStream = File.OpenRead(filename))
                {
                    byte[] value = md.ComputeHash(fileStream);
                    result = BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
                }
            }
            return result;
        }

        public static void error(string message)
        {
            // CORREGIDO: Adaptado para usar el MessageBox de Windows Forms de tu proyecto
            System.Windows.Forms.MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }

        private static async Task<string> req(NameValueCollection post_data)
        {
            try
            {
                var formData = new List<KeyValuePair<string, string>>();
                foreach (string key in post_data)
                {
                    formData.Add(new KeyValuePair<string, string>(key, post_data[key]));
                }
                var content = new FormUrlEncodedContent(formData);

                var handler = new HttpClientHandler
                {
                    Proxy = null,
                    ServerCertificateCustomValidationCallback = (request, certificate, chain, sslPolicyErrors) => true
                };

                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    HttpResponseMessage response = await client.PostAsync("https://keyauth.win/api/1.3/", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        error("Connection failure to license server.");
                        TerminateProcess(GetCurrentProcess(), 1);
                        return "";
                    }

                    string raw_response = await response.Content.ReadAsStringAsync();
                    return raw_response;
                }
            }
            catch (Exception ex)
            {
                error("Connection failure: " + ex.Message);
                TerminateProcess(GetCurrentProcess(), 1);
                return "";
            }
        }

        #region app_data
        public app_data_class app_data = new app_data_class();
        public class app_data_class
        {
            public string numUsers { get; set; }
            public string numOnlineUsers { get; set; }
            public string numKeys { get; set; }
            public string version { get; set; }
            public string customerPanelLink { get; set; }
            public string downloadLink { get; set; }
        }
        private void load_app_data(app_data_structure data)
        {
            app_data.numUsers = data.numUsers;
            app_data.numOnlineUsers = data.numOnlineUsers;
            app_data.numKeys = data.numKeys;
            app_data.version = data.version;
            app_data.customerPanelLink = data.customerPanelLink;
        }
        #endregion

        #region user_data
        public user_data_class user_data = new user_data_class();
        public class user_data_class
        {
            public string username { get; set; }
            public string ip { get; set; }
            public string hwid { get; set; }
            public string createdate { get; set; }
            public string lastlogin { get; set; }
            public List<Data> subscriptions { get; set; }
        }
        public class Data
        {
            public string subscription { get; set; }
            public string expiry { get; set; }
            public string timeleft { get; set; }
            public string key { get; set; }
        }
        private void load_user_data(user_data_structure data)
        {
            user_data.username = data.username;
            user_data.ip = data.ip;
            user_data.hwid = data.hwid;
            user_data.createdate = data.createdate;
            user_data.lastlogin = data.lastlogin;
            user_data.subscriptions = data.subscriptions;
        }
        #endregion

        [DataContract]
        private class TwoFactorData
        {
            [DataMember(Name = "secret_code")] public string SecretCode { get; set; }
            [DataMember(Name = "QRCode")] public string QRCode { get; set; }
        }

        #region response_struct
        public response_class response = new response_class();
        public class response_class
        {
            public bool success { get; set; }
            public string message { get; set; }
        }
        private void load_response_struct(response_structure data)
        {
            response.success = data.success;
            response.message = data.message;
        }
        #endregion

        private json_wrapper response_decoder = new json_wrapper(new response_structure());
    }

    #region Criptografia Corregida
    public static class encryption
    {
        public static byte[] str_to_byte_arr(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }

    public class json_wrapper
    {
        public static bool is_serializable(Type to_check) => true;
        public json_wrapper(object obj_to_work_with)
        {
            current_object = obj_to_work_with;
            serializer = new DataContractJsonSerializer(current_object.GetType());
        }

        public object string_to_object(string json)
        {
            var buffer = Encoding.Default.GetBytes(json);
            using (var mem_stream = new MemoryStream(buffer))
                return serializer.ReadObject(mem_stream);
        }

        public T string_to_generic<T>(string json) => (T)string_to_object(json);
        private DataContractJsonSerializer serializer;
        private object current_object;
    }
    #endregion
}