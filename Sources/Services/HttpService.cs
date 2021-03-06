﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ApigeeSDK.Exceptions;

namespace ApigeeSDK.Services
{
    public class HttpService
    {
        private readonly HttpClient _http;

        public HttpService(HttpClient http)
        {
            _http = http;
        }

        public virtual async Task<string> PostAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            IEnumerable<KeyValuePair<string, string>> formContent)
        {
            return await SendAsync(HttpMethod.Post, url, headers, formContent);
        }

        public virtual async Task<string> DeleteAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            IEnumerable<KeyValuePair<string, string>> formContent)
        {
            return await SendAsync(HttpMethod.Delete, url, headers, formContent);
        }

        public virtual async Task<string> PostFileAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            string filePath)
        {
            var form = new MultipartFormDataContent();
            form.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");

            ByteArrayContent fileContent;

            await using (var fs = File.OpenRead(filePath))
            {
                var streamContent = new StreamContent(fs);
                fileContent = new ByteArrayContent(await streamContent.ReadAsByteArrayAsync());
            }

            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/zip");

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            return await SendAsync(HttpMethod.Post, url, headers, form);
        }

        public virtual async Task<string> PostJsonAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            string json)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await SendAsync(HttpMethod.Post, url, headers, content);
        }

        public virtual async Task<string> GetAsync(string url,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            return await SendAsync(HttpMethod.Get, url, headers, (HttpContent)null);
        }

        private async Task<string> SendAsync(
            HttpMethod method,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            IEnumerable<KeyValuePair<string, string>> formContent
            )
        {
            HttpContent content = null;
            if (formContent != null)
            {
                content = new FormUrlEncodedContent(formContent);
            }

            return await SendAsync(method, url, headers, content);
        }

        private async Task<string> SendAsync(
            HttpMethod method,
            string url,
            IEnumerable<KeyValuePair<string, string>> headers,
            HttpContent content)
        {
            var request = new HttpRequestMessage(method, url);

            if (content != null)
            {
                request.Content = content;
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    switch (header.Key.ToLower(CultureInfo.InvariantCulture))
                    {
                        case "content-type":
                            request.Content ??= new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());
                            request.Content.Headers.ContentType = new MediaTypeHeaderValue(header.Value);

                            break;
                        case "authorization":
                            request.Headers.Authorization = AuthenticationHeaderValue.Parse(header.Value);
                            break;
                        default:
                            request.Headers.Add(header.Key, header.Value);
                            break;
                    }
                }
            }

            HttpResponseMessage response;

            try
            {
                response = await _http.SendAsync(request);
            }
            catch (TaskCanceledException e)
                when (e.InnerException is IOException &&
                      e.Source == "System.Net.Http")
            {
                throw new ApigeeSdkTimeoutException("Operation timeout.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content != null 
                    ? await response.Content.ReadAsStringAsync() 
                    : string.Empty;
                throw new ApigeeSdkHttpException(response.StatusCode, body);
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}