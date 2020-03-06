using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;
using System.Configuration;

namespace inRiver.Connectors.EPiServer.Communication
{
    public class RestEndpoint<T>
    {
        private readonly string endpointAddress;

        private readonly string action;

        private readonly string apikey;

        private readonly Dictionary<string, string> settingsDictionary;

        private readonly int timeout;

        private readonly bool enableEndPoint;

        private readonly inRiverContext _context;

        public RestEndpoint(string endpointAddress, string action, inRiverContext context)
        {
            this.action = action;
            this.endpointAddress = this.ValidateEndpointAddress(endpointAddress);
            this.timeout = 1;
            this.enableEndPoint = true;
            _context = context;
        }

        public RestEndpoint(Dictionary<string, string> settings, string action, inRiverContext context)
        {
            this._context = context;
            this.action = action;
            this.settingsDictionary = settings;

            if (settings.ContainsKey("EPI_APIKEY"))
            {
                this.apikey = settings["EPI_APIKEY"];
            }
            else
            {
                throw new ConfigurationErrorsException("Missing EPI_APIKEY setting on connector. It needs to be defined to else the calls will fail. Please see the documentation.");
            }

            if (settings.ContainsKey("EPI_ENDPOINT_URL") == false)
            {
                throw new ConfigurationErrorsException("Missing EPI_ENDPOINT_URL setting on connector. It should point to the import end point on the EPiServer Commerce web site. Please see the documentation.");
            }

            this.endpointAddress = this.ValidateEndpointAddress(settings["EPI_ENDPOINT_URL"]);

            if (settings.ContainsKey("EPI_RESTTIMEOUT"))
            {
                string timeoutString = settings["EPI_RESTTIMEOUT"];
                if (!int.TryParse(timeoutString, out this.timeout))
                {
                    throw new ConfigurationErrorsException("Can't parse EPI_RESTTIMEOUT : " + timeoutString);
                }
            }
            else
            {
                throw new ConfigurationErrorsException("Missing EPI_RESTTIMEOUT setting on connector. It needs to be defined to else the calls will fail. Please see the documentation.");
            }

            this.enableEndPoint = true;
            if (settings.ContainsKey("ENABLE_EPI_ENDPOINT"))
            {
                string valueEnableEPIEndpoint = settings["ENABLE_EPI_ENDPOINT"];

                if (!string.IsNullOrEmpty(valueEnableEPIEndpoint))
                {
                    this.enableEndPoint = bool.Parse(valueEnableEPIEndpoint);
                }
            }
        }

        public string Action
        {
            get { return this.action; }
        }

        public string GetUrl()
        {
            return this.GetUrl(this.action);
        }

        public string GetUrl(string action)
        {
            string endpointAddress = this.endpointAddress;

            if (string.IsNullOrEmpty(action) == false)
            {
                endpointAddress = endpointAddress + action;
            }

            return endpointAddress;
        }

        public string Post(T message)
        {
            if (!enableEndPoint)
            {
                _context.Log(LogLevel.Information, "EPI Endpoint Disabled under Configuration");
                return string.Empty;
            }

            Uri uri = new Uri(this.GetUrl());
            HttpClient client = new HttpClient();
            string baseUrl = uri.Scheme + "://" + uri.Authority;

            _context.Log(LogLevel.Information, $"Posting to {uri}");

            client.BaseAddress = new Uri(baseUrl);

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apikey", this.apikey);

            // HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            client.Timeout = new TimeSpan(this.timeout, 0, 0);
            HttpResponseMessage response = client.PostAsJsonAsync(uri.PathAndQuery, message).Result;
            if (response.IsSuccessStatusCode)
            {
                 // Parse the response body. Blocking!
                string resp = response.Content.ReadAsAsync<string>().Result;

                _context.Log(LogLevel.Debug, $"Post response - Message:{message} Result:{resp} URI:{uri.PathAndQuery}");

                int tries = 0;
                RestEndpoint<string> endpoint = new RestEndpoint<string>(this.settingsDictionary, "IsImporting", this._context);

                while (resp == "importing")
                {
                    tries++;
                    if (tries < 10)
                    {
                        Thread.Sleep(2000);
                    }
                    else if (tries < 30)
                    {
                        Thread.Sleep(30000);
                    }
                    else
                    {
                        Thread.Sleep(300000);
                    }

                    resp = endpoint.Get();

                    if (tries == 1 || (tries % 5 == 0))
                        _context.Log(LogLevel.Debug, $"Post - GET - Message:{message} Retries:{tries} Response:{resp} URI:{uri.PathAndQuery}");
                }

                if (resp.StartsWith("ERROR"))
                {
                    _context.Log(LogLevel.Error, resp);
                }

                return resp;
            }

            string errorMsg = string.Format("Import failed: {0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            _context.Log(LogLevel.Error, errorMsg);
            throw new HttpRequestException(errorMsg);
        }

        public string Get()
        {
            if (!enableEndPoint)
            {
                _context.Log(LogLevel.Information, "EPI Endpoint Disabled under Configuration");
                return string.Empty;
            }

            Uri uri = new Uri(this.GetUrl());
            HttpClient client = new HttpClient();
            string baseUrl = uri.Scheme + "://" + uri.Authority;

            client.BaseAddress = new Uri(baseUrl);

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apikey", this.apikey);

            // HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            client.Timeout = new TimeSpan(this.timeout, 0, 0);
            HttpResponseMessage response = client.GetAsync(uri.PathAndQuery).Result;

            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                string resp = response.Content.ReadAsAsync<string>().Result;

                return resp;

            }
            else
            {
                string errorMsg = string.Format("Import failed: {0} ({1})", (int)response.StatusCode,
                    response.ReasonPhrase);
                _context.Log(LogLevel.Error,
                    errorMsg);
                throw new HttpRequestException(errorMsg);
            }
        }

        public List<string> PostWithStringListAsReturn(T message)
        {
            if (!enableEndPoint)
            {
                _context.Log(LogLevel.Information, "EPI Endpoint Disabled under Configuration");
                return null;
            }

            Uri uri = new Uri(this.GetUrl());
            HttpClient client = new HttpClient();
            string baseUrl = uri.Scheme + "://" + uri.Authority;

            _context.Log(LogLevel.Debug,
                string.Format("Posting to {0}", uri.ToString()));

            client.BaseAddress = new Uri(baseUrl);

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apikey", this.apikey);

            // HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            client.Timeout = new TimeSpan(this.timeout, 0, 0);
            HttpResponseMessage response = client.PostAsJsonAsync<T>(uri.PathAndQuery, message).Result;
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                return response.Content.ReadAsAsync<List<string>>().Result;
            }
            else
            {
                string errorMsg = string.Format("Import failed: {0} ({1})", (int)response.StatusCode,
                    response.ReasonPhrase);
                _context.Log(LogLevel.Error,
                    errorMsg);
                throw new HttpRequestException(errorMsg);
            }
        }

        private string ValidateEndpointAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ConfigurationErrorsException("Missing ImportEndPointAddress setting on connector. It should point to the import end point on the EPiServer Commerce web site. Please see the documentation.");
            }

            if (address.EndsWith("/") == false)
            {
                return address + "/";
            }

            return address;
        }
    }
}