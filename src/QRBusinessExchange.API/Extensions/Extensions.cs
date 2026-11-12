using QRBusinessExchange.API.Models;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace QRBusinessExchange.API.Extensions
{
	public static class Extensions
	{
		public static StringBuilder sbLog = new StringBuilder();

		public static string CurrentActivity { get; set; }

		public static string Encode(this string value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
		}

		public static string Decode(this string value)
		{
			if (value.Contains("\"")) { value = value.Replace("\"", string.Empty); }
			byte[] byteValue = Convert.FromBase64String(value);
			return Encoding.UTF8.GetString(byteValue);
		}

		public static string ATOBEncode(this string value)
		{
			return Convert.ToBase64String(Encoding.GetEncoding(28591).GetBytes(value));
		}

		public static string BTOADecode(this string value)
		{
			if (value.Contains("\"")) { value = value.Replace("\"", string.Empty); }
			return Encoding.GetEncoding(28591).GetString(Convert.FromBase64String(value));
		}

		//public static string HashEncryption(this string value)
		//{
		//    byte[] salt = new byte[16];
		//    using (RandomNumberGenerator rngCsp = RandomNumberGenerator.Create())
		//    {
		//        rngCsp.GetNonZeroBytes(salt);
		//    }
		//    Console.WriteLine("Salt: " + Convert.ToBase64String(salt));
		//    return Convert.ToBase64String(KeyDerivation.Pbkdf2(value, salt, KeyDerivationPrf.HMACSHA256, 100000, 32));
		//}

		//public static string PasswordHasher(this string value)
		//{
		//    int saltSize = 16;
		//    int bytesRequired = 32;
		//    byte[] array = new byte[1 + saltSize + bytesRequired];
		//    int iterations = 100000; // 1000, afaik, which is the min recommended for Rfc2898DeriveBytes
		//    using (var pbkdf2 = new Rfc2898DeriveBytes(value, array, iterations))
		//    {
		//        byte[] salt = pbkdf2.Salt;
		//        Buffer.BlockCopy(salt, 0, array, 1, saltSize);
		//        byte[] bytes = pbkdf2.GetBytes(bytesRequired);
		//        Buffer.BlockCopy(bytes, 0, array, saltSize + 1, bytesRequired);
		//    }
		//    return Convert.ToBase64String(array);
		//}

		public static string HtmlEncode(this string value)
		{
			return WebUtility.HtmlEncode(value);
		}

		public static string HtmlDecode(this string value)
		{
			return WebUtility.HtmlDecode(value);
		}

		public static string GenerateBatchID(this int value)
		{
			return string.Format("{0}{1}", DateTime.Now.ToString("yyyyMMdd"), value.ToString("D3"));
		}

		public static string Serialize<T>(this T value)
		{
			JsonSerializerSettings settings = new JsonSerializerSettings
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore
			};
			return JsonConvert.SerializeObject(value, Formatting.None, settings);
		}

		public static T Deserialize<T>(this string value)
		{
			return JsonConvert.DeserializeObject<T>(value);
		}

		public static object DeserializeToObject(this string value)
		{
			return JsonConvert.DeserializeObject<object>(value);
		}

		public static Guid ToGuid(this string value)
		{
			return Guid.Parse(value);
		}

		public static int ToInt(this string value)
		{
			int.TryParse(value, out var res);
			return res;
		}

		public static int[] toIntArray(this string value)
		{
			string[] splitted = value.Replace("[", string.Empty).Replace("]", string.Empty).Split(',');
			int[] nums = new int[splitted.Length];
			for (int i = 0; i < splitted.Length; i++)
			{
				nums[i] = int.Parse(splitted[i]);
			}
			return nums;
		}

		public static string ToCapitalize(this string value)
		{
			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
		}

		public static double ToJulianDate(this DateTime date)
		{
			return date.ToOADate() + 2415018.5;
		}

		public static List<string> StringToStringList(this string strObject)
		{
			List<string> list = new List<string>();

			if (strObject.Contains(';'))
			{
				foreach (string str in strObject.Split(';'))
				{
					list.Add(str);
				}
			}
			else
			{
				list.Add(strObject);
			}

			return list;
		}

		public static string GetValueFromArray(this string[] values, string search)
		{
			for (int i = 0; i < values.Length; i++)
			{
				string[] items = values[i].Split('=');
				if (items[0].Contains(search))
				{
					return items[1];
				}
			}
			return null;
		}

		public static string SafeReplace(this string input, string find, string replace, bool matchWholeWord)
		{
			string textToFind = matchWholeWord ? string.Format(@"(?<!\S){0}(?!\S)", Regex.Escape(find)) : find;
			return Regex.Replace(input, textToFind, replace.Replace("$", "$$"));
		}
		public static string ReplaceWholeWord(this string original, string wordToFind, string replacement, RegexOptions regexOptions = RegexOptions.None)
		{
			string pattern = $"\\b{wordToFind}\\b";
			return Regex.Replace(original, pattern, replacement, regexOptions);
		}

		public static string ReplaceLastWordInUrl(this string original, string replacement)
		{
			string res = string.Empty;
			string[] orgStringArray = original.Split('/');
			foreach (string s in orgStringArray)
			{
				if (s.Contains("pdf"))
				{
					res = Path.Combine(res, replacement);
				}
				else
				{
					res = Path.Combine(res, s);
				}
			}
			return res.Replace('\\', '/');
		}
		public static void UpdateImportFile(this string filename, string searchtext, string replacetext)
		{
			string text = System.IO.File.ReadAllText(filename);
			text = text.Replace(searchtext, replacetext);
			System.IO.File.WriteAllText($"{filename.Split('.')[0]}_upd.sql", text);
		}

		public static string GetMACAddress()
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface nic in allNetworkInterfaces)
			{
				if (nic.OperationalStatus == OperationalStatus.Up)
				{
					return AddressBytesToString(nic.GetPhysicalAddress().GetAddressBytes());
				}
			}
			return string.Empty;
		}

		public static IPAddressModel GetMyIPAddress()
		{
			string? url = "IPAddress_Validation_URL".ReadFromConfiguration("");
			if (string.IsNullOrEmpty(url))
				throw new InvalidOperationException("Configuration value for 'IPAddress_Validation_URL' is missing.");
			return HttpReceive<IPAddressModel>(url);
		}

		//public static DevicesJson GetMyIPAddressDomainServerDetails(string url)
		//{
		//    return HttpReceive<DevicesJson>(url);
		//}

		public static string ParseUnicodeHex(this string hex)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hex.Length; i += 4)
			{
				char character = (char)Convert.ToInt16(hex.Substring(i, 4), 16);
				sb.Append(character);
			}
			return sb.ToString();
		}

		public static string GetApplicationBaseDirectory(this string BaseDirectory)
		{
			string result = string.Empty;
			string[] array = BaseDirectory.Split('\\');
			foreach (string folder in array)
			{
				if (folder.Equals("bin"))
				{
					break;
				}
				result = result + folder + "\\";
			}
			return result;
		}

		private static string AddressBytesToString(byte[] addressBytes)
		{
			return string.Join(":", addressBytes.Select(delegate (byte b)
			{
				byte b2 = b;
				return b2.ToString("X2");
			}).ToArray());
		}

		public static string GetCurrentMethod(this string value)
		{
			return new StackTrace().GetFrame(1).GetMethod().Name;
		}

		//public static RemoteConnection GetResultFromURL(string url)
		//{
		//    try
		//    {
		//        HttpClient httpClient = new HttpClient(new HttpClientHandler
		//        {
		//            UseDefaultCredentials = true
		//        });
		//        string data = string.Empty;
		//        HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
		//        using (HttpResponseMessage response = httpClient.Send(message))
		//        {
		//            Stream streamResponse = response.Content.ReadAsStream();
		//            string characterSet = response.Content.Headers.ContentType.CharSet;
		//            if (response.StatusCode == HttpStatusCode.OK)
		//            {
		//                StreamReader readStream = null;
		//                readStream = ((!string.IsNullOrWhiteSpace(characterSet)) ? new StreamReader(streamResponse, Encoding.GetEncoding(characterSet)) : new StreamReader(streamResponse));
		//                data = readStream.ReadToEnd();
		//                readStream.Close();
		//            }
		//        }
		//        return data.Deserialize<RemoteConnection>();
		//    }
		//    catch (Exception ex)
		//    {
		//        ex.ErrorLog();
		//        return null;
		//    }
		//}

		public static void WriteToJsonFile<T>(string filePath, T objectToWrite) where T : new()
		{
			TextWriter writer = null;
			try
			{
				string contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite);
				writer = new StreamWriter(filePath);
				writer.Write(contentsToWriteToFile);
			}
			finally
			{
				writer?.Close();
			}
		}

		public static void WriteToFile(string filePath, string content, bool append)
		{
			TextWriter writer = null;
			try
			{
				if (append)
				{
					writer = new StreamWriter(filePath, append: true);
					writer.Write(content + System.Environment.NewLine);
				}
				else
				{
					writer = new StreamWriter(filePath);
					writer.Write(content);
				}
			}
			finally
			{
				writer?.Close();
			}
		}

		public static void MsgLog(this string msg, string msgType)
		{
			if (!string.IsNullOrEmpty(msg))
			{
				string LogDir = System.AppDomain.CurrentDomain.BaseDirectory;
				int loc = LogDir.IndexOf("bin");
				if (loc != -1) { LogDir = LogDir.Substring(0, loc); }
				LogDir = $"{LogDir}logs";

				if (!System.IO.Directory.Exists(LogDir))
				{
					System.IO.Directory.CreateDirectory(LogDir);
				}

				string path = $"{LogDir}/errorLog{DateTime.Now.ToString("yyyy-MM-dd")}.txt";
				string errorMessage = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss tt")} [{msgType}] :: {msg}{System.Environment.NewLine}";

				System.IO.File.AppendAllText(path, errorMessage);
			}
		}
		public static void ErrorLog(this Exception ex)
		{
			if (ex != null)
			{
				string LogDir = System.AppDomain.CurrentDomain.BaseDirectory;
				int loc = LogDir.IndexOf("bin");
				if (loc != -1) { LogDir = LogDir.Substring(0, loc); }
				LogDir = $"{LogDir}logs";

				if (!System.IO.Directory.Exists(LogDir))
				{
					System.IO.Directory.CreateDirectory(LogDir);
				}

				string path = $"{LogDir}/errorLog{DateTime.Now.ToString("yyyy-MM-dd")}.txt";
				string errorMessage = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss tt")} [ERR] :: {ex.Message} :: {ex.StackTrace}{System.Environment.NewLine}";

				System.IO.File.AppendAllText(path, errorMessage);
			}
		}

		public static void InformationTrack(this string information)
		{
			if (!string.IsNullOrEmpty(information))
			{
				string LogDir = System.AppDomain.CurrentDomain.BaseDirectory;
				int loc = LogDir.IndexOf("bin");
				if (loc != -1) { LogDir = LogDir.Substring(0, loc); }
				LogDir = $"{LogDir}logs";

				if (!System.IO.Directory.Exists(LogDir))
				{
					System.IO.Directory.CreateDirectory(LogDir);
				}

				string path = $"{LogDir}/errorLog{DateTime.Now.ToString("yyyy-MM-dd")}.txt";
				string infoMessage = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss tt")} [INF] :: {information}{System.Environment.NewLine}";

				System.IO.File.AppendAllText(path, infoMessage);
			}
		}

		public static void WriteToFile(this string value, string path)
		{
			try
			{
				using StreamWriter writer = new StreamWriter(path, append: false);
				writer.WriteLine(value);
				writer.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
		public static string? ReadFromConfiguration(this string key, string section = "")
		{
			ConfigurationBuilder build = (ConfigurationBuilder)new ConfigurationBuilder().AddJsonFile("appsettings.json");
			var cnf = build.Build();
			var res = (string.IsNullOrEmpty(section)) ? cnf.GetValue<string>(key) : cnf.GetSection($"{section}:{key}").Value;
			return res;
		}
		public static List<string> ReadFromFile(this string filePath)
		{
			List<string> result = new List<string>();
			TextReader reader = null;
			try
			{
				reader = new StreamReader(filePath);
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					result.Add(line);
				}
				return result;
			}
			finally
			{
				reader?.Close();
			}
		}
		public static string ReadSQLFromFile(this string filePath)
		{
			TextReader reader = null;
			try
			{
				reader = new StreamReader(filePath);
				return reader.ReadToEnd();
			}
			finally
			{
				reader?.Close();
			}
		}

		public static T HttpReceive<T>(string url)
		{
			string url2 = url;
			T result = Activator.CreateInstance<T>();
			new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");
			Task.WaitAll(Task.Run(delegate
			{
				using HttpClient httpClient = new HttpClient();
				Task<string> stringAsync = httpClient.GetStringAsync(url2);
				result = stringAsync.Result.Deserialize<T>();
			}));
			return result;
		}

        public static async Task<T> HttpReceiveAsync<T>(string url)
        {
            string stringAsync = string.Empty;
            T result = Activator.CreateInstance<T>();
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(url);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            stringAsync = await httpClient.GetStringAsync(url);
            result = stringAsync.Deserialize<T>();
            return result;
        }

        public static async Task<string> HttpReceiveValueAsync(string url)
        {
            string stringAsync = string.Empty;
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(url);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            stringAsync = await httpClient.GetStringAsync(url);
            return stringAsync;
        }

        public static string HttpGet(string url)
		{
			string result = string.Empty;
			Task.WaitAll(Task.Run(delegate
			{
				using HttpClient httpClient = new HttpClient();
				Task<string> stringAsync = httpClient.GetStringAsync(url);
				result = stringAsync.Result;
			}));
			return result;
		}

		public static T HttpPost<T>(this string value, Uri uri)
		{
			Uri uri2 = uri;
			string value2 = value;
			T result = Activator.CreateInstance<T>();
			new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");
			Task.WaitAll(Task.Run(delegate
			{
				using HttpClient httpClient = new HttpClient();
				HttpResponseMessage result2 = httpClient.PostAsync(uri2, new StringContent(value2, Encoding.UTF8, "application/json")).Result;
				result2.EnsureSuccessStatusCode();
				Task<string> task = result2.Content.ReadAsStringAsync();
				result = task.Result.Deserialize<T>();
			}));
			return result;
		}

		public static T HttpPost<T>(this T value, Uri uri)
		{
			Uri uri2 = uri;
			string value2 = value.Serialize<T>();
			T result = Activator.CreateInstance<T>();
			new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");

			Task.WaitAll(Task.Run(delegate
			{
				using HttpClient httpClient = new HttpClient();
				httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				httpClient.DefaultRequestHeaders.Add("connectionlog", $"{value.Serialize<T>()}");
				HttpResponseMessage result2 = httpClient.PostAsync(uri2, new StringContent(value2, Encoding.UTF8, "application/json")).Result;
				result2.EnsureSuccessStatusCode();
				Task<string> task = result2.Content.ReadAsStringAsync();
				result = task.Result.Deserialize<T>(); //.Contains("{")) ? task.Result.Replace("\\","").Replace("[", "").Replace("]", "").Deserialize<List<T>>() : default(List<T>);
			}));

			return result;
		}

		//public static void HttpPost(this Uri uri, List<HeaderParameters> headerParameters = null)
		//{
		//    List<HeaderParameters> headerParameters2 = headerParameters;
		//    Uri uri2 = uri;
		//    Task.WaitAll(Task.Run(delegate
		//    {
		//        using HttpClient httpClient = new HttpClient();
		//        StringContent stringContent = new StringContent(string.Empty, Encoding.UTF8, "application/json");
		//        if (headerParameters2 != null)
		//        {
		//            foreach (HeaderParameters current in headerParameters2)
		//            {
		//                stringContent.Headers.Add(current.key, current.value);
		//            }
		//        }
		//        HttpResponseMessage result = httpClient.PostAsync(uri2, stringContent).Result;
		//        result.EnsureSuccessStatusCode();
		//        result.Content.ReadAsStringAsync();
		//    }));
		//}

		public static T HttpDelete<T>(string url)
		{
			string url2 = url;
			T result = Activator.CreateInstance<T>();
			new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");
			Task.WaitAll(Task.Run(delegate
			{
				using HttpClient httpClient = new HttpClient();
				HttpResponseMessage result2 = httpClient.GetAsync(url2).Result;
				result2.EnsureSuccessStatusCode();
				Task<string> task = result2.Content.ReadAsStringAsync();
				result = task.Result.Deserialize<T>();
			}));
			return result;
		}

		public static async Task HttpPost(this string value, Uri uri)
		{
			using HttpClient client = new HttpClient();
			HttpResponseMessage obj = await client.PostAsync(uri, new StringContent(value, Encoding.UTF8, "application/json"));
			obj.EnsureSuccessStatusCode();
			await obj.Content.ReadAsStringAsync();
		}

		//public static async Task<HttpResponseMessage> HttpResponsePostAsync(this HttpClient client, string value, string url)
		//{
		//    HttpResponseMessage obj = await client.PostAsync(new Uri(url), new StringContent(value, Encoding.UTF8, "application/json"));
		//    obj.EnsureSuccessStatusCode();
		//    return obj; //await obj.Content.ReadAsStringAsync();
		//}

		public static async Task<HttpResponseMessage?> HttpResponsePostAsync(this HttpClient client, string Url, string Data)
		{
			try
			{
				//var Values = new List<KeyValuePair<string, string>>();
				//Values.Add(new KeyValuePair<string, string>("",""));
				//var content = new FormUrlEncodedContent(Values);
				client.BaseAddress = new Uri(Url);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.Add("Data", Data);

				HttpResponseMessage response = await client.PostAsync(Url, null); //.PostAsync(Url, content);
				if (response.IsSuccessStatusCode)
				{
					return response;
				}
				return null;
			}
			catch (Exception ex)
			{
				ex.ErrorLog();
				return null;
			}
		}

		//public static HttpResponseMessage HttpResponsePost(this HttpClient client, string Url, string Data)
		//{
		//    try
		//    {
		//        var Values = new List<KeyValuePair<string, string>>();
		//        Values.Add(new KeyValuePair<string, string>("", ""));
		//        var content = new FormUrlEncodedContent(Values);
		//        client.BaseAddress = new Uri(Url);
		//        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		//        client.DefaultRequestHeaders.Add("Data", Data);

		//        HttpResponseMessage response = client.PostAsync(Url, content).Result;
		//        if (response.IsSuccessStatusCode)
		//        {
		//            return response;
		//        }
		//        return null;
		//    }
		//    catch (Exception ex)
		//    {
		//        ex.ErrorLog();
		//        return null;
		//    }
		//}

		public static string HttpResponsePost(this string uri, List<KeyValuePair<string, string>> pairs)
		{
			var client = new HttpClient();

			//var content = new FormUrlEncodedContent(pairs);

			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			client.DefaultRequestHeaders.Add("values", $"{pairs.Serialize<List<KeyValuePair<string, string>>>()}");

			var response = client.PostAsync(uri, null).Result;
			Stream receiveStream = response.Content.ReadAsStream();
			StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
			return readStream.ReadToEnd();
		}

		//public static bool HttpPost<T>(string api_url)
		//{
		//    string api_url2 = api_url;
		//    T result = Activator.CreateInstance<T>();
		//    new StringContent(JsonConvert.SerializeObject(result), Encoding.UTF8, "application/json");
		//    Task.WaitAll(Task.Run(delegate
		//    {
		//        using HttpClient httpClient = new HttpClient();
		//        StringContent content = new StringContent("{ userName: machagus, password: xxxxxxx }", Encoding.UTF8, "application/json");
		//        HttpResponseMessage result2 = httpClient.PostAsync(api_url2, content).Result;
		//        result2.EnsureSuccessStatusCode();
		//        Task<string> task = result2.Content.ReadAsStringAsync();
		//        result = task.Result.Deserialize<T>();
		//    }));
		//    return true;
		//}

		public static string GetSettingByName(this string configKeyName)
		{
			try
			{
				ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
				configurationBuilder.AddJsonFile("appsettings.json");
				return configurationBuilder.Build().GetSection(configKeyName).Value;
			}
			catch (Exception)
			{
				throw;
			}
		}

		//public static void SetCookie(this HttpResponse response, string key, string value, int timeLength = 15, int timelife = 0)
		//{
		//    CookieOptions option = new CookieOptions();

		//    switch (timelife)
		//    {
		//        case 0:
		//            option.Expires = DateTime.Now.AddSeconds(timeLength);
		//            break;
		//        case 1:
		//            option.Expires = DateTime.Now.AddMinutes(timeLength);
		//            break;
		//        case 2:
		//            option.Expires = DateTime.Now.AddHours(timeLength);
		//            break;
		//        case 3:
		//            option.Expires = DateTime.Now.AddDays(timeLength);
		//            break;
		//        case 4:
		//            option.Expires = DateTime.Now.AddYears(timeLength);
		//            break;
		//        default:
		//            break;
		//    }

		//    option.Secure = true;
		//    option.SameSite = SameSiteMode.Strict;
		//    option.IsEssential = true;

		//    response.Cookies.Append(key, value, option);
		//}

		//public static string GetCookie(this HttpRequest request, string key)
		//{
		//    return request.Cookies[key];
		//}

		//public static void RemoveCookie(this HttpResponse response, string key)
		//{
		//    response.Cookies.Delete(key);
		//}

		public static string UnsafeRelaxedJsonEscaping(this string value)
		{
			if (value.Contains("\\u0022")) { value = value.Replace("\\u0022", string.Empty); }
			var serializeOptions = new JsonSerializerOptions
			{
				WriteIndented = true,
				Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			};

			string res = System.Text.Json.JsonSerializer.Serialize(value, serializeOptions);
			if (res.Contains("\"")) { res = res.Replace("\"", "").Replace("\\", ""); }
			return res;
		}

		public static string RemoveHiddenChars(this string input)
		{
			return new string(input.Where(c => char.IsLetter(c) || char.IsDigit(c)).ToArray());
		}

		public static bool ContainsColumn(this IDataReader @this, int columnIndex)
		{
			try
			{
				return @this.FieldCount > columnIndex;
			}
			catch (Exception)
			{
				try
				{
					return @this[columnIndex] != null;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}

		public static bool ContainsColumn(this IDataReader @this, string columnName)
		{
			try
			{
				return @this.GetOrdinal(columnName) != -1;
			}
			catch (Exception)
			{
				try
				{
					return @this[columnName] != null;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}
	}
}
