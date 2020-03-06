namespace inRiver.EPiServerCommerce.Twelve.Importer
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Controllers;

    public class SecuredApiController : ApiController
    {
        private const string ApiKeyName = "apikey";

        private static string ApiKeyValue
        {
            get
            {
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    string apikey = string.Empty;
                    string environmentName = ConfigurationManager.AppSettings["episerver:EnvironmentName"];
                    if (AzureFileManager.UseAzure && !string.IsNullOrEmpty(environmentName))
                    {
                        apikey = ConfigurationManager.AppSettings[environmentName + "." + "inRiver.apikey"];
                    }
                    else
                    {
                        apikey = ConfigurationManager.AppSettings["inRiver.apikey"];
                    }
                    if (apikey != null)
                    {
                        return apikey;
                    }

                    return null;
                }

                return null;
            }
        }

        public override Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            if (ValidateApiKey(controllerContext.Request) == false)
            {
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.Forbidden);
                TaskCompletionSource<HttpResponseMessage> tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(resp);
                return tsc.Task;
            }

            return base.ExecuteAsync(controllerContext, cancellationToken);
        }

        protected virtual bool ValidateApiKey(HttpRequestMessage request)
        {
            string apiKey = null;
            if (request.Headers.Contains(ApiKeyName))
            {
                // Validate api key
                apiKey = request.Headers.GetValues(ApiKeyName).FirstOrDefault();
            }

            if (string.Compare(apiKey, ApiKeyValue, StringComparison.InvariantCultureIgnoreCase) != 0)
            {
                return false;
            }

            return true;
        }
    }
}
