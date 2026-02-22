// ***********************************
// Dotmim.Sync.Web.Client
// WebRemoteOrchestrator.cs
// Modified: 02/22/2026 (Larry Leach)
// ***********************************

using Dotmim.Sync.Extensions;
using Dotmim.Sync.Serialization;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.DecompressionMethods;

namespace Dotmim.Sync.Web.Client
{
    public partial class WebRemoteOrchestrator : RemoteOrchestrator
    {
        private static readonly TimeSpan DefaultSyncTimeout = TimeSpan.FromMinutes(20);

        private readonly Dictionary<string, string> _customHeaders = [];
        private readonly Dictionary<string, string> _scopeParameters = [];

        public WebRemoteOrchestrator(
            string serviceUri,
            IConverter customConverter = null,
            HttpClient client = null,
            SyncPolicy syncPolicy = null,
            int maxDownloadingDegreeOfParallelism = 4,
            string identifier = null)
            : this(serviceUri == null ? null : new Uri(serviceUri), customConverter, client, syncPolicy,
                maxDownloadingDegreeOfParallelism, identifier)
        {
        }

        public WebRemoteOrchestrator(
            Uri serviceUri,
            IConverter customConverter = null,
            HttpClient client = null,
            SyncPolicy syncPolicy = null,
            int maxDownloadingDegreeOfParallelism = 4,
            string identifier = null)
            : base(null, new SyncOptions())
        {
            // if no HttpClient provisioned, create a new one
            if (client == null)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                var handler = new HttpClientHandler();
#pragma warning restore CA2000 // Dispose objects before losing scope

                // Activated by default
                if (handler.SupportsAutomaticDecompression)
                {
                    handler.AutomaticDecompression = GZip | Deflate;
                }

                this.HttpClient = new HttpClient(handler);
            }
            else
            {
                this.HttpClient = client;
            }

            this.SyncPolicy = this.EnsurePolicy(syncPolicy);
            this.Converter = customConverter;
            this.MaxDownloadingDegreeOfParallelism =
                maxDownloadingDegreeOfParallelism <= 0 ? -1 : maxDownloadingDegreeOfParallelism;
            this.ServiceUri = serviceUri;
            this.SerializerFactory = SerializersFactory.JsonSerializerFactory;
            this.Identifier = identifier;
        }

        public HttpClient HttpClient
        {
            get => field ??= CreateHttpClient();
            set
            {
                field = value ?? throw new ArgumentNullException(nameof(value));
                EnsureTimeout(field);
            }
        }

        public string Identifier { get; set; }

        public IConverter Converter { get; set; }

        public int MaxDownloadingDegreeOfParallelism { get; }

        public ISerializerFactory SerializerFactory { get; set; }

        public SyncPolicy SyncPolicy { get; set; }

        public Uri ServiceUri { get; set; }

        public CookieHeaderValue Cookie { get; private set; }

        private static void EnsureTimeout(HttpClient client)
        {
            // bump anything “default-ish” or clearly too small for first sync/provisioning
            if (client.Timeout == TimeSpan.FromSeconds(100) || client.Timeout < TimeSpan.FromMinutes(5))
            {
                client.Timeout = DefaultSyncTimeout;
            }
        }

        private static HttpClient CreateHttpClient()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var handler = new HttpClientHandler();
#pragma warning restore CA2000 // Dispose objects before losing scope

            handler.AutomaticDecompression = GZip | Deflate;
            return new HttpClient(handler, true) { Timeout = DefaultSyncTimeout };
        }

        public string GetServiceHost()
        {
            return this.ServiceUri == null ? "Undefined" : this.ServiceUri.Host;
        }

        public static bool TryGetHeaderValue(HttpResponseHeaders n, string key, out string header)
        {
            if (n.TryGetValues(key, out var vs))
            {
                header = vs.First();
                return true;
            }

            header = null;
            return false;
        }

        public static Task<string> ReadContentFromResponseAsync(HttpResponseMessage response)
        {
            return response.Content?.ReadAsStringAsync();
        }

        public void AddScopeParameter(string key, string value)
        {
            this._scopeParameters[key] = value;
        }

        public void AddCustomHeader(string key, string value)
        {
            this._customHeaders[key] = value;
        }

        public async Task<T> ProcessRequestAsync<T>(SyncContext context, IScopeMessage message, HttpStep step,
            int batchSize,
            IProgress<ProgressArgs> progress = null, CancellationToken cancellationToken = default)
            where T : IScopeMessage
        {
            ArgumentNullException.ThrowIfNull(context);

            Guard.ThrowIfNull(this.HttpClient);
            Guard.ThrowIfNull(this.ServiceUri, "ServiceUri is not defined");

            HttpResponseMessage response = null;
            Stream streamResponse = null;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Execute my OpenAsync in my policy context
                response = await this.SyncPolicy
                    .ExecuteAsync(async ct => await this.SendAsync(step, message, batchSize, ct).ConfigureAwait(false),
                        progress, cancellationToken).ConfigureAwait(false);

                // Ensure we have a cookie
                this.EnsureCookie(response?.Headers);

                if (response?.Content == null)
                {
                    throw new HttpEmptyResponseContentException();
                }

                T messageResponse = default;
                var serializer = this.SerializerFactory.GetSerializer();

#if NET6_0_OR_GREATER
                streamResponse = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                streamResponse = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif

                if (streamResponse.CanRead)
                {
                    messageResponse = await serializer.DeserializeAsync<T>(streamResponse).ConfigureAwait(false);
                }

                context = messageResponse?.SyncContext;

                await this.InterceptAsync(
                    new HttpGettingResponseMessageArgs(response, this.ServiceUri,
                        HttpStep.SendChangesInProgress, context, messageResponse, this.GetServiceHost()), progress,
                    cancellationToken).ConfigureAwait(false);

                return messageResponse;
            }
            catch (HttpSyncWebException)
            {
                throw;
            }
            catch (SyncException se)
            {
                throw new HttpSyncWebException(se.Message);
            }
            catch (Exception e)
            {
                if (response?.Content == null || streamResponse == null)
                {
                    throw new HttpSyncWebException(e.Message);
                }

                var exError = await ReadContentFromResponseAsync(response).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(exError))
                {
                    throw new HttpSyncWebException(e.Message);
                }
                else
                {
                    throw new HttpSyncWebException(exError);
                }
            }
            finally
            {
                if (streamResponse != null)
                {
                    await streamResponse.DisposeAsync().ConfigureAwait(false);
                }

                response?.Dispose();
            }
        }

        public async Task<HttpResponseMessage> ProcessRequestAsync(IScopeMessage message, HttpStep step, int batchSize,
            IProgress<ProgressArgs> progress = null, CancellationToken cancellationToken = default)
        {
            Guard.ThrowIfNull(this.HttpClient);
            Guard.ThrowIfNull(this.ServiceUri, "ServiceUri is not defined");

            HttpResponseMessage response = null;
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Execute my OpenAsync in my policy context
                response = await this.SyncPolicy
                    .ExecuteAsync(async ct => await this.SendAsync(step, message, batchSize, ct).ConfigureAwait(false),
                        progress, cancellationToken).ConfigureAwait(false);

                // Ensure we have a cookie
                this.EnsureCookie(response?.Headers);

                return response?.Content == null ? throw new HttpEmptyResponseContentException() : response;
            }
            catch (HttpSyncWebException)
            {
                throw;
            }
            catch (SyncException se)
            {
                throw new HttpSyncWebException(se.Message);
            }
            catch (Exception e)
            {
                if (response?.Content == null)
                {
                    throw new HttpSyncWebException(e.Message);
                }

                var exError = await ReadContentFromResponseAsync(response).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(exError))
                {
                    throw new HttpSyncWebException(e.Message);
                }

                throw new HttpSyncWebException(exError);
            }
        }

        public override string ToString()
        {
            return this.GetServiceHost();
        }

        private static async Task SerializeAsync(HttpResponseMessage response, string fileName,
            string directoryFullPath)
        {
            if (!Directory.Exists(directoryFullPath))
            {
                Directory.CreateDirectory(directoryFullPath!);
            }

            var fullPath = Path.Combine(directoryFullPath, fileName);
            var streamResponse = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var streamResponse1 = streamResponse.ConfigureAwait(false);
            var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.ReadWrite);
            await using var stream = fileStream.ConfigureAwait(false);
            await streamResponse.CopyToAsync(fileStream).ConfigureAwait(false);
        }

        private static async Task<HttpMessageSendChangesResponse> DeserializeAsync(ISerializerFactory serializerFactory,
            string fileName, string directoryFullPath, BaseOrchestrator orchestrator = null)
        {
            var fullPath = Path.Combine(directoryFullPath, fileName);
            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            await using var stream = fileStream.ConfigureAwait(false);
            var httpMessageContent = await serializerFactory.GetSerializer()
                .DeserializeAsync<HttpMessageSendChangesResponse>(fileStream).ConfigureAwait(false);
            return httpMessageContent;
        }

        private async Task HandleSyncErrorAsync(HttpResponseMessage response)
        {
            try
            {
                // read the content as exception from the response
                var exceptionString = await ReadContentFromResponseAsync(response).ConfigureAwait(false);

                if (string.IsNullOrEmpty(exceptionString))
                {
                    exceptionString = response.ReasonPhrase;
                }

                // Invoke response failure Interceptors to handle the failed response
                await this.InvokeResponseFailureInterceptors(response, exceptionString).ConfigureAwait(false);

                HttpSyncWebException syncException = null;

                if (!TryGetHeaderValue(response.Headers, "dotmim-sync-error", out var syncErrorTypeName))
                {
                    syncException = new HttpSyncWebException(exceptionString);
                }
                else
                {
                    // Error are always json formatted
                    var webSyncErrorSerializer = new JsonObjectSerializer();

                    WebSyncException webError = null;
                    try
                    {
                        webError = webSyncErrorSerializer.Deserialize<WebSyncException>(exceptionString);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (webError != null)
                    {
                        var exceptionMessageString = webError.Message;

                        if (string.IsNullOrEmpty(exceptionMessageString))
                        {
                            exceptionMessageString = response.ReasonPhrase;
                        }

                        syncException = new HttpSyncWebException(exceptionMessageString)
                        {
                            DataSource = webError.DataSource,
                            InitialCatalog = webError.InitialCatalog,
                            Number = webError.Number,
                            SyncStage = webError.SyncStage,
                            TypeName = webError.TypeName
                        };
                    }
                    else
                    {
                        syncException = new HttpSyncWebException(response.ReasonPhrase);
                    }
                }

                syncException.ReasonPhrase = response.ReasonPhrase;
                syncException.StatusCode = response.StatusCode;

                throw syncException;
            }
            catch (SyncException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SyncException(ex);
            }
        }

        private SyncPolicy EnsurePolicy(SyncPolicy policy)
        {
            if (policy != null)
            {
                return policy;
            }

            // Defining my retry policy
            policy = SyncPolicy.WaitAndRetry(
                2,
                retryNumber => TimeSpan.FromMilliseconds(500 * retryNumber),
                (ex, arg) => ex is not SyncException { TypeName: nameof(HttpSessionLostException) },
                async (ex, cpt, ts, arg) =>
                {
                    await this.InterceptAsync(new HttpSyncPolicyArgs(10, cpt, ts, this.GetServiceHost()))
                        .ConfigureAwait(false);
                });

            return policy;
        }

        private Uri BuildUri(Uri baseUri)
        {
            var requestUri = new StringBuilder();
            var baseUriString = baseUri.AbsoluteUri;
            requestUri.Append(baseUri.AbsoluteUri);
            requestUri.Append(baseUriString.EndsWith('/') ? string.Empty : "/");

            // Add params if any
            if (this._scopeParameters is not { Count: > 0 })
            {
                return new Uri(requestUri.ToString());
            }

            var prefix = "?";
            foreach (var kvp in this._scopeParameters)
            {
                requestUri.Append(CultureInfo.InvariantCulture,
                    $"{prefix}{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                if (prefix.Equals("?", StringComparison.Ordinal))
                {
                    prefix = "&";
                }
            }

            return new Uri(requestUri.ToString());
        }

        private void EnsureCookie(HttpResponseHeaders headers)
        {
            if (headers == null)
            {
                return;
            }

            if (!headers.TryGetValues("Set-Cookie", out var tmpList))
            {
                return;
            }

            var cookieList = tmpList.ToList();

            // var cookieList = response.Headers.GetValues("Set-Cookie").ToList();
            if (cookieList.Count <= 0)
            {
                return;
            }

            // try to parse the very first cookie
            if (CookieHeaderValue.TryParse(cookieList[0], out var cookie))
            {
                this.Cookie = cookie;
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpStep step, IScopeMessage message, int batchSize,
            CancellationToken cancellationToken)
        {
            var serializer = this.SerializerFactory.GetSerializer();

            var contentType = this.SerializerFactory.Key == SerializersFactory.JsonSerializerFactory.Key
                ? "application/json"
                : null;
            var serializerInfo =
                new SerializerInfo
                {
                    SerializerKey = this.SerializerFactory.Key, ClientBatchSize = batchSize
                }; //new SerializerInfo(this.SerializerFactory.Key, batchSize);

            // using json to serialize header
            var jsonSerializer = new JsonObjectSerializer();
            var serializerInfoJsonBytes = await jsonSerializer.SerializeAsync(serializerInfo).ConfigureAwait(false);

            var requestUri = this.BuildUri(this.ServiceUri);

            // Create the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);

            // Adding the serialization format used and session id and scope name
            requestMessage.Headers.Add("dotmim-sync-session-id", message.SyncContext.SessionId.ToString());
            requestMessage.Headers.Add("dotmim-sync-scope-id", message.SyncContext.ClientId.ToString());
            requestMessage.Headers.Add("dotmim-sync-scope-name", message.SyncContext.ScopeName);
            requestMessage.Headers.Add("dotmim-sync-step", ((int)step).ToString(CultureInfo.InvariantCulture));
            requestMessage.Headers.Add("dotmim-sync-serialization-format", serializerInfoJsonBytes.ToUtf8String());
            requestMessage.Headers.Add("dotmim-sync-version", SyncVersion.Current.ToString());

            if (!string.IsNullOrEmpty(this.Identifier))
            {
                requestMessage.Headers.Add("dotmim-sync-identifier", this.Identifier);
            }

            // if client specifies a converter, add it as header
            if (this.Converter != null)
            {
                requestMessage.Headers.Add("dotmim-sync-converter", this.Converter.Key);
            }

            // Adding others headers
            if (this._customHeaders is { Count: > 0 })
            {
                foreach (var kvp in this._customHeaders.Where(kvp => !requestMessage.Headers.Contains(kvp.Key)))
                {
                    requestMessage.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            var args = new HttpSendingRequestMessageArgs(requestMessage, message.SyncContext, message,
                this.GetServiceHost());
            await this.InterceptAsync(args, null, cancellationToken).ConfigureAwait(false);

            var binaryData = await serializer.SerializeAsync(args.Data).ConfigureAwait(false);
            requestMessage = args.Request;

            // Check if data is null
            binaryData ??= [];

            // calculate hash
            var hash = HashAlgorithm.SHA256.Create(binaryData);
            var hashString = Convert.ToBase64String(hash);
            requestMessage.Headers.Add("dotmim-sync-hash", hashString);

            // get byte array content
            requestMessage.Content = new ByteArrayContent(binaryData);

            // If Json, specify header
            if (!string.IsNullOrEmpty(contentType) && !requestMessage.Content.Headers.Contains("content-type"))
            {
                requestMessage.Content.Headers.Add("content-type", contentType);
            }

            HttpResponseMessage response;
            try
            {
                // Eventually, send the request
                response = await this.HttpClient
                    .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new HttpSyncWebException(ex.Message);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            // throw exception if response is not successful
            // get response from server
            if (!response.IsSuccessStatusCode)
            {
                // handle the synchronization error
                await this.HandleSyncErrorAsync(response).ConfigureAwait(false);
            }

            return response;
        }

        /// <remarks>
        ///     Response failure Interceptors provide a mechanism for executing custom logic
        ///     when an HTTP response indicates failure (non-success status codes).
        ///     Interceptors may include logging, error handling, retry logic, or other actions
        ///     to be taken upon encountering failed responses from API calls.
        /// </remarks>
        private async Task InvokeResponseFailureInterceptors(HttpResponseMessage response, string exceptionString)
        {
            // Check if there are any Interceptors registered for HttpResponseFailureArgs
            if (!this.HasInterceptors<HttpResponseFailureArgs>())
            {
                return; // No Interceptors registered, so return early
            }

            // Extract necessary details from the HttpResponseMessage
            var statusCode = (int)response.StatusCode;
            var reasonPhrase = response.ReasonPhrase;
            var headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            var requestUri = response.RequestMessage!.RequestUri;

            // Create and return a new instance of HttpResponseFailureArgs
            // Construct HttpResponseFailureArgs instance with details of the failed response
            var failureArgs =
                new HttpResponseFailureArgs(statusCode, reasonPhrase, exceptionString, headers, requestUri);

            // Invoke Interceptors asynchronously, allowing custom logic to be executed
            await this.InterceptAsync(failureArgs).ConfigureAwait(false);
        }
    }
}