using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Generators;
using OpenQA.Selenium;
using System.Runtime.ConstrainedExecution;
using Org.BouncyCastle.Utilities;
using System.Collections.Concurrent;
using Durandal.Common.Collections;
using System.IO;
using Durandal.Common.IO;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class RequestInterceptingHttpProxy : IHttpServerDelegate
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly IThreadPool _httpThreadPool;
        private readonly RawTcpSocketServer _socketServer;
        private readonly SocketHttpServer _httpServer;
        private readonly FastConcurrentHashSet<InterceptedHttpRequest> _interceptedRequests;

        public RequestInterceptingHttpProxy(
            ILogger logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _httpClientFactory = httpClientFactory.AssertNonNull(nameof(httpClientFactory));
            _httpThreadPool = new TaskThreadPool();
            _interceptedRequests = new FastConcurrentHashSet<InterceptedHttpRequest>();

            ServerBindingInfo[] endpoints = new ServerBindingInfo[]
                {
                    new ServerBindingInfo("*", port: null, certificateId: null, supportHttp2: false)
                };

            _socketServer = new RawTcpSocketServer(
                endpoints,
                logger,
                DefaultRealTimeProvider.Singleton,
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty,
                new WeakPointer<IThreadPool>(_httpThreadPool));
            
            _httpServer = new SocketHttpServer(_socketServer,
                logger,
                new DefaultRandom(),
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty);

            _httpServer.RegisterSubclass(this);
        }

        public async Task Start()
        {
            await _socketServer.StartServer("HttpProxy", CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            await _httpServer.StartServer("HttpProxy", CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        }

        public async Task Stop()
        {
            await _httpServer.StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
            await _socketServer.StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
        }

        public IEnumerable<InterceptedHttpRequest> OutgoingRequests => _interceptedRequests;

        public void ClearRequestCache()
        {
            _interceptedRequests.Clear();
        }

        public Uri LocalUri
        {
            get
            {
                return _httpServer.LocalAccessUri;
            }
        }

        private static string GetWildcardedAuthorityName(string authority)
        {
            if (string.IsNullOrEmpty(authority) || !authority.Contains('.'))
            {
                return authority;
            }

            return "*" + authority.Substring(authority.IndexOf('.'));
        }

        public async Task HandleConnection(IHttpServerContext context, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                if (string.Equals(HttpConstants.HTTP_VERB_CONNECT, context.HttpRequest.RequestMethod) &&
                    context.CurrentProtocolVersion == HttpVersion.HTTP_1_1)
                {
                    using (IHttpClient client = _httpClientFactory.CreateHttpClient(new Uri("https://" + context.HttpRequest.RequestFile)))
                    {
                        client.InitialProtocolVersion = HttpVersion.HTTP_1_1;
                        WeakPointer<ISocket> socket = (WeakPointer<ISocket>)((FieldInfo)(typeof(SocketHttpServerContext1_1).GetMember("_socket", BindingFlags.NonPublic | BindingFlags.Instance)[0])).GetValue(context);

                        // Pose as the target server using a Fiddler SSL cert to establish a TLS connection
                        string targetAuthority = GetWildcardedAuthorityName(client.ServerAddress.Authority);
                        X509Certificate2 fiddlerCert;
                        if (!CertificateCache.Instance.TryGetCertificate(CertificateIdentifier.BySubjectName(targetAuthority), out fiddlerCert))
                        {
                            _logger.Log("No site certificate found for " + targetAuthority + "; run Fiddler first to generate cert", Durandal.Common.Logger.LogLevel.Err);
                            await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse(), NullLogger.Singleton, cancelToken, realTime).ConfigureAwait(false);
                            return;
                        }

                        HttpResponse initialResponse = HttpResponse.OKResponse();
                        string connectionHeader;
                        if (context.HttpRequest.RequestHeaders.TryGetValue("Proxy-Connection", out connectionHeader) &&
                            string.Equals(connectionHeader, HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase))
                        {
                            initialResponse.ResponseHeaders["Proxy-Connection"] = HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE;
                        }

                        await context.WritePrimaryResponse(initialResponse, NullLogger.Singleton, cancelToken, realTime).ConfigureAwait(false);
                        SslStream sslStream = new SslStream(new SocketStream(socket, NullLogger.Singleton, false));
                        await sslStream.AuthenticateAsServerAsync(fiddlerCert);
                        StreamSocket unencryptedSocket = new StreamSocket(sslStream);

                        try
                        {
                            bool stayConnected = true;
                            while (stayConnected)
                            {
                                using (HttpRequest outgoingChromeRequest = await HttpHelpers.ReadRequestFromSocket(
                                    unencryptedSocket,
                                    HttpVersion.HTTP_1_1,
                                    _logger.Clone("ProxyRead"),
                                    cancelToken,
                                    realTime).ConfigureAwait(false))
                                {
                                    //outgoingChromeRequest.RemoteHost = "localhost";
                                    stayConnected =
                                        (outgoingChromeRequest.RequestHeaders.TryGetValue("Proxy-Connection", out connectionHeader) &&
                                            string.Equals(connectionHeader, HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase)) ||
                                        (outgoingChromeRequest.RequestHeaders.TryGetValue("Connection", out connectionHeader) &&
                                            string.Equals(connectionHeader, HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase));
                                    outgoingChromeRequest.RequestHeaders.Remove("Proxy-Connection");
                                    outgoingChromeRequest.RequestHeaders.Remove("Accept-Encoding"); // don't compress any responses so we can inspect them in cleartext later
                                    InterceptedHttpRequest interceptedRequest = new InterceptedHttpRequest()
                                    {
                                        Host = client.ServerAddress.ToString().TrimEnd('/'),
                                        RequestPath = outgoingChromeRequest.RequestFile,
                                        GetParameters = outgoingChromeRequest.GetParameters,
                                        RequestData = new byte[0],
                                        ResponseData = new byte[0],
                                    };

                                    _logger.Log($"Intercepted outgoing HTTPS request {interceptedRequest.Host}{interceptedRequest.RequestPath}", Durandal.Common.Logger.LogLevel.Vrb);

                                    outgoingChromeRequest.MakeProxied();
                                    using (HttpResponse wireResponse = await client.SendRequestAsync(outgoingChromeRequest).ConfigureAwait(false))
                                    {
                                        wireResponse.MakeProxied();

                                        // Capture all the incoming response data
                                        byte[] capturedResponseData;
                                        using (MemoryStream dataInCapture = new MemoryStream())
                                        {
                                            await wireResponse.GetOutgoingContentStream().CopyToAsyncPooled(dataInCapture, cancelToken);
                                            capturedResponseData = dataInCapture.ToArray();
                                        }

                                        if (capturedResponseData.Length > 0)
                                        {
                                            wireResponse.SetContent(capturedResponseData, wireResponse.ResponseHeaders["Content-Type"]);
                                        }

                                        //string x = Encoding.UTF8.GetString(capturedResponseData);
                                        interceptedRequest.ResponseData = capturedResponseData;
                                        interceptedRequest.ResponseCode = wireResponse.ResponseCode;
                                        _interceptedRequests.Add(interceptedRequest);

                                        stayConnected = stayConnected &&
                                            (wireResponse.ResponseHeaders.TryGetValue("Connection", out connectionHeader) &&
                                                string.Equals(connectionHeader, HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase));

                                        await HttpHelpers.WriteResponseToSocket(
                                            wireResponse,
                                            HttpVersion.HTTP_1_1,
                                            unencryptedSocket,
                                            cancelToken,
                                            realTime,
                                            _logger.Clone("ProxyWrite"),
                                            () => $"Proxy to {client.ServerAddress}").ConfigureAwait(false);
                                        await wireResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                        await unencryptedSocket.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                        catch (NullReferenceException) { }
                        catch (Exception e)
                        {
                            if (!e.Message.Contains("ocket") && !(e is SocketException))
                            {
                                _logger.Log(e);
                            }
                        }
                    }
                }
                else
                {
                    using (IHttpClient client = _httpClientFactory.CreateHttpClient(new Uri(context.HttpRequest.RequestFile)))
                    {
                        InterceptedHttpRequest interceptedRequest = new InterceptedHttpRequest()
                        {
                            Host = client.ServerAddress.ToString().TrimEnd('/'),
                            RequestPath = context.HttpRequest.RequestFile,
                            GetParameters = context.HttpRequest.GetParameters,
                        };

                        _interceptedRequests.Add(interceptedRequest);
                        _logger.Log($"Intercepted outgoing HTTP request {interceptedRequest.Host}{interceptedRequest.RequestPath}", Durandal.Common.Logger.LogLevel.Vrb);
                        context.HttpRequest.MakeProxied();
                        context.HttpRequest.RequestHeaders.Remove("Proxy-Connection");
                        context.HttpRequest.RequestFile = new Uri(context.HttpRequest.RequestFile).AbsolutePath;

                        using (HttpResponse wireResponse = await client.SendRequestAsync(context.HttpRequest).ConfigureAwait(false))
                        {
                            wireResponse.MakeProxied();
                            await context.WritePrimaryResponse(wireResponse, _logger, cancelToken, realTime);
                            await wireResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("ocket") && !(e is SocketException))
                {
                    _logger.Log(e);
                }
            }
        }
    }
}
