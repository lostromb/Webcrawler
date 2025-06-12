using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public static class ConferenceDownloaderChromium
    {
        private static string[] ALL_CHURCH_LANGUAGES = new string[] { "orm", "afr", "ase", "apw", "asf", "aym", "ind", "msa", "bam", "tzo", "bik", "bis", "bfi", "cak", "cat", "ceb", "ces", "cha", "nya", "cym", "dan", "deu", "nav", "cuk", "yor", "est", "efi", "guz", "eng", "spa", "eus", "ton", "fat", "hif", "chk", "fon", "fra", "smo", "tvl", "grn", "hil", "hmn", "hrv", "haw", "sto", "ibo", "ilo", "nbl", "xho", "zul", "isl", "ita", "kos", "mah", "qvi", "kam", "kin", "gil", "swa", "niu", "hat", "lav", "lit", "lin", "yua", "hun", "pon", "mlg", "mlt", "mam", "rar", "meu", "nld", "bla", "cag", "nor", "pau", "pam", "pag", "pap", "pol", "por", "ept", "kek", "quh", "quc", "tah", "ron", "nso", "tsn", "sna", "alb", "ssw", "slk", "slv", "sot", "fin", "swe", "tgl", "mri", "yap", "vie", "tpi", "lua", "tur", "twi", "fij", "war", "quz", "ell", "bel", "bul", "kaz", "mkd", "mon", "rus", "srp", "ukr", "kat", "hyw", "hye", "urd", "ara", "pes", "amh", "nep", "hin", "ben", "tam", "tel", "kan", "mal", "sin", "tha", "lao", "ksw", "mya", "khm", "kor", "jpn", "cmn-Latn", "zhs", "zho", "yue" };
        private static string[] CONFERENCE_LANGUAGES = new string[] { "apw", "asf", "aym", "ind", "msa", "bis", "cak", "ceb", "ces", "dan", "deu", "nav", "cuk", "yor", "est", "efi", "eng", "spa", "ton", "fat", "hif", "chk", "fra", "smo", "grn", "hil", "hmn", "hrv", "ibo", "ilo", "isl", "ita", "kos", "mah", "gil", "swa", "hat", "lav", "lit", "lin", "hun", "pon", "mlg", "nld", "nor", "pau", "pap", "pol", "por", "ept", "kek", "quc", "tah", "ron", "slk", "fin", "swe", "tgl", "yap", "vie", "tpi", "twi", "fij", "ell", "bul", "mon", "rus", "srp", "ukr", "kat", "hye", "urd", "ara", "pes", "amh", "nep", "hin", "tam", "tel", "sin", "tha", "lao", "mya", "khm", "kor", "jpn", "zho", "yue" };

        public static async Task DownloadConference()
        {
            Console.OutputEncoding = Encoding.UTF8;
            ILogger logger = new ConsoleLogger("WebCrawler", Durandal.Common.Logger.LogLevel.Std | Durandal.Common.Logger.LogLevel.Wrn | Durandal.Common.Logger.LogLevel.Err);

            string downloadTargetDirectory = @"C:\Code\WebCrawler\bin\staging";
            if (!Directory.Exists(downloadTargetDirectory))
            {
                logger.Log("Creating staging directory " + downloadTargetDirectory);
                Directory.CreateDirectory(downloadTargetDirectory);
            }

            DirectoryInfo stagingDirectory = new DirectoryInfo(downloadTargetDirectory);
            ConcurrentQueue<WebClient> sharedWebClients = new ConcurrentQueue<WebClient>();

            Queue<ConferenceDownloadJob> conferencesToDownload = new Queue<ConferenceDownloadJob>();

            //IHttpClient httpClient = new PortableHttpClient(
            //    "www.churchofjesuschrist.org", 443, true, logger.Clone("HttpClient"),
            //    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
            //    DimensionSet.Empty);

            //List<string> supportedLanguages = new List<string>();
            //foreach (string langCode in ALL_CHURCH_LANGUAGES)
            //{
            //    try
            //    {
            //        LanguageCode lang = LanguageCode.TryParse(langCode);
            //        if (lang == null)
            //        {
            //            logger.Log("Could not parse lang code " + langCode, Durandal.Common.Logger.LogLevel.Err);
            //            lang = LanguageCode.CreateCustom(null, langCode);
            //        }

            //        string url = string.Format("/study/general-conference/2023/10?lang={0}",
            //            lang.ToBcp47Alpha3String());
            //        using (var resp = await httpClient.SendRequestAsync(HttpRequest.CreateOutgoing(url, "HEAD")))
            //        {
            //            if (resp.ResponseCode == 200)
            //            {
            //                supportedLanguages.Add(langCode);
            //            }
            //        }
            //    }
            //    catch (Exception e) { logger.Log(e); }
            //}

            //Console.WriteLine("Supported langs: \"" + string.Join("\", \"", supportedLanguages) + "\"");

            //string[] CONFERENCE_LANGS_TO_DOWNLOAD = CONFERENCE_LANGUAGES;
            string[] CONFERENCE_LANGS_TO_DOWNLOAD = new string[] { "eng" };
            //for (int year = 2024; year > 1970; year--)
            //{
            //    conferencesToDownload.Enqueue(new ConferenceDownloadJob(year, ConferencePhase.October, LanguageCode.ENGLISH));
            //    conferencesToDownload.Enqueue(new ConferenceDownloadJob(year, ConferencePhase.April, LanguageCode.ENGLISH));
            //}
            //for (int loop = 0; loop < 5; loop++)
            //{
            //    foreach (string lang in CONFERENCE_LANGS_TO_DOWNLOAD)
            //    {
            //        for (int year = 2024; year >= 1971; year--)
            //        {
            //            LanguageCode langCode = LanguageCode.TryParse(lang);
            //            if (langCode == null)
            //            {
            //                langCode = LanguageCode.CreateCustom(null, lang);
            //            }

            //            conferencesToDownload.Enqueue(new ConferenceDownloadJob(year, ConferencePhase.October, langCode));
            //            conferencesToDownload.Enqueue(new ConferenceDownloadJob(year, ConferencePhase.April, langCode));
            //        }
            //    }
            //}

            for (int loop = 0; loop < 5; loop++)
            {
                foreach (string lang in CONFERENCE_LANGS_TO_DOWNLOAD)
                {
                    LanguageCode langCode = LanguageCode.TryParse(lang);
                    if (langCode == null)
                    {
                        langCode = LanguageCode.CreateCustom(null, lang);
                    }

                    conferencesToDownload.Enqueue(new ConferenceDownloadJob(2025, ConferencePhase.April, langCode));
                }
            }

            //conferencesToDownload.Enqueue(new ConferenceDownloadJob(2024, ConferencePhase.October, LanguageCode.ENGLISH));
            //conferencesToDownload.Enqueue(new ConferenceDownloadJob(2022, ConferencePhase.April, LanguageCode.FRENCH));
            //conferencesToDownload.Enqueue(new ConferenceDownloadJob(2023, ConferencePhase.April, LanguageCode.VIETNAMESE));
            //conferencesToDownload.Enqueue(new ConferenceDownloadJob(2011, ConferencePhase.October, LanguageCode.Parse("pol")));
            //conferencesToDownload.Enqueue(new ConferenceDownloadJob(2011, ConferencePhase.October, LanguageCode.CreateCustom(null, "apw")));


            // See if chromium exists
            string chromeBinaryLoc = Environment.CurrentDirectory + "\\chrome";
            if (!File.Exists(chromeBinaryLoc + "\\chrome.exe"))
            {
                if (!File.Exists(Environment.CurrentDirectory + "\\chrome.zip"))
                {
                    logger.Log("Chrome.zip not found!", Durandal.Common.Logger.LogLevel.Err);
                    return;
                }

                logger.Log("Unpacking chrome binary");
                IFileSystem localFileSystem = new RealFileSystem(logger.Clone("FileSystem"));
                using (ZipFile chromiumZip = new ZipFile(new VirtualPath("chrome.zip"), logger.Clone("ChromeUnpacker"), localFileSystem))
                {
                    chromiumZip.ExtractAll(VirtualPath.Root);
                }
            }

            // Start a local HTTP proxy server to intercept chrome's traffic
            ISocketFactory socketFactory = new TcpClientSocketFactory(
                logger.Clone("SocketFactory"));
            IHttpClientFactory downstreamClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty,
                Http2SessionManager.Default);
            RequestInterceptingHttpProxy httpProxy = new RequestInterceptingHttpProxy(
                NullLogger.Singleton,//logger.Clone("RequestInterceptor"),
                downstreamClientFactory);
            await httpProxy.Start();
            logger.Log("Hosting HTTP proxy at " + httpProxy.LocalUri.ToString());
            string proxyUrlWithoutScheme = "localhost:" + httpProxy.LocalUri.Port.ToString();

            logger.Log("Creating chrome driver");
            ChromeOptions chromeOptions = new ChromeOptions()
            {
                PageLoadStrategy = PageLoadStrategy.Normal,
                BinaryLocation = chromeBinaryLoc + "\\chrome.exe",
                LeaveBrowserRunning = false,
                AcceptInsecureCertificates = true,
                Proxy = new Proxy()
                {
                    IsAutoDetect = false,
                    Kind = ProxyKind.Manual,
                    SslProxy = proxyUrlWithoutScheme,
                    HttpProxy = proxyUrlWithoutScheme,
                },
            };

            using (FixedCapacityThreadPool backgroundThreadPool = new FixedCapacityThreadPool(
                new TaskThreadPool(),
                logger.Clone("EncodingThreadPool"),
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "EncodingThreadPool",
                Environment.ProcessorCount - 1))
            {
                while (conferencesToDownload.Count > 0)
                {
                    try
                    {
                        await RunDownloadJob(conferencesToDownload, logger, stagingDirectory, sharedWebClients, backgroundThreadPool, chromeBinaryLoc, chromeOptions, httpProxy);
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                    finally
                    {
                        foreach (var proc in Process.GetProcessesByName("chrome"))
                        {
                            try
                            {
                                proc.Kill();
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }
        }

        private static async Task RunDownloadJob(
            Queue<ConferenceDownloadJob> conferencesToDownload,
            ILogger logger,
            DirectoryInfo stagingDirectory,
            ConcurrentQueue<WebClient> sharedWebClients,
            IThreadPool backgroundThreadPool,
            string chromeBinaryLoc,
            ChromeOptions chromeOptions,
            RequestInterceptingHttpProxy httpProxy)
        {
            const int downloadsLimitPerTab = 50;
            int downloadsPerformed = 0;
            using (ChromeDriverService chromeDriverService = ChromeDriverService.CreateDefaultService(chromeBinaryLoc, "chromedriver.exe"))
            {
                ChromeDriver webDriver = new ChromeDriver(chromeDriverService, chromeOptions);
                try
                {
                    //webDriver.Manage().Window.Maximize();
                    while (conferencesToDownload.Count > 0)
                    {
                        ConferenceDownloadJob job = conferencesToDownload.Dequeue();
                        logger.Log("Running batch job for " + job.Year + " " + job.Phase + " " + job.Language.ToBcp47Alpha3String());

                        // Get localized strings first if needed
                        LocalizedStrings localeStrings;
                        if (!LocalizedStrings.GLOBAL_STRING_DICT.TryGetValue(job.Language, out localeStrings))
                        {
                            localeStrings = await FigureOutStringsForLocale(job.Language, webDriver, logger, httpProxy).ConfigureAwait(false);
                            if (localeStrings == null)
                            {
                                logger.Log("No localization info. Cannot proceed", Durandal.Common.Logger.LogLevel.Err);
                                continue;
                            }

                            LocalizedStrings.GLOBAL_STRING_DICT[job.Language] = localeStrings;
                        }

                        IList<ConferenceTalk> talks = await PreProcessJob(job, webDriver, logger, httpProxy);
                        logger.Log("Discovered " + talks.Count + " talks in this conference");
                        int sequentialLinkFailures = 0;
                        foreach (ConferenceTalk talk in talks)
                        {
                            if (sequentialLinkFailures >= 4)
                            {
                                logger.Log("Giving up trying to load this conference - too many missing download links", Durandal.Common.Logger.LogLevel.Wrn);
                                break;
                            }

                            // See if we have this talk already
                            if (File.Exists(TalkProcessor.GetFilePathOpusOrdinalNaming(stagingDirectory, talk)) &&
                                File.Exists(TalkProcessor.GetFilePathM4AOrdinalNaming(stagingDirectory, talk)))
                            {
                                logger.Log("Encoded file for " + talk.ToString() + " already exists, skipping", Durandal.Common.Logger.LogLevel.Vrb);
                                continue;
                            }

                            if (!talk.HasVideoThumbnail)
                            {
                                continue;
                            }

                            logger.Log("Inspecting talk " + talk.ToString());
                            httpProxy.ClearRequestCache();
                            string downloadLink = await GetDownloadLink(talk, webDriver, logger, httpProxy);

                            downloadsPerformed++;
                            if (downloadsPerformed >= downloadsLimitPerTab)
                            {
                                // Refresh the chrome instance so we don't run out of memory
                                bool webDriverOk = false;
                                int recycleTries = 4;
                                logger.Log("Recycling Chrome instance...");
                                do
                                {
                                    try
                                    {
                                        webDriver.Quit();
                                        webDriver = new ChromeDriver(chromeDriverService, chromeOptions);
                                        webDriver.Navigate().GoToUrl("https://www.churchofjesuschrist.org");
                                        downloadsPerformed = 0;
                                        webDriverOk = true;
                                    }
                                    catch (Exception) { }
                                } while (!webDriverOk && recycleTries-- > 0);
                            }

                            if (string.IsNullOrWhiteSpace(downloadLink))
                            {
                                logger.Log("Could not parse download link", Durandal.Common.Logger.LogLevel.Err);
                                sequentialLinkFailures++;
                                continue;
                            }

                            sequentialLinkFailures = -99;
                            logger.Log("Parsed download link: " + downloadLink, Durandal.Common.Logger.LogLevel.Vrb);
                            BackgroundEncodeTaskClosure closure = new BackgroundEncodeTaskClosure()
                            {
                                talkInfo = talk,
                                webClientPool = sharedWebClients,
                                downloadLink = downloadLink,
                                stagingDir = stagingDirectory,
                                logger = logger,
                                localeStrings = localeStrings,
                            };

                            backgroundThreadPool.EnqueueUserAsyncWorkItem(closure.Run);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e);
                }
                finally
                {
                    try
                    {
                        webDriver.Quit();
                    }
                    catch (Exception) { }

                    logger.Log("Waiting for background encodes to finish...");
                    while (backgroundThreadPool.TotalWorkItems > 0)
                    {
                        await backgroundThreadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                }
            }
        }

        private class BackgroundEncodeTaskClosure
        {
            public string downloadLink;
            public ConferenceTalk talkInfo;
            public ConcurrentQueue<WebClient> webClientPool;
            public DirectoryInfo stagingDir;
            public ILogger logger;
            public LocalizedStrings localeStrings;

            public async Task Run()
            {
                WebClient webClient;
                if (!webClientPool.TryDequeue(out webClient))
                {
                    webClient = new WebClient();
                }

                await TalkProcessor.DownloadTalkAndConvert(
                    talkInfo,
                    webClient,
                    downloadLink,
                    stagingDir,
                    logger,
                    localeStrings);

                webClientPool.Enqueue(webClient);
            }
        }

        private static async Task<IList<ConferenceTalk>> PreProcessJob(
            ConferenceDownloadJob job,
            ChromeDriver webDriver,
            ILogger logger,
            RequestInterceptingHttpProxy httpProxy)
        {
            List<ConferenceTalk> returnVal = new List<ConferenceTalk>();

            httpProxy.ClearRequestCache();
            webDriver.Navigate().GoToUrl(job.PageUrl);

            // Detect a 404 page first
            if (httpProxy.OutgoingRequests.Any((req) =>
                req.ResponseCode == 404 &&
                string.Equals(req.RequestPath, job.PageUrl.LocalPath)
            ))
            {
                logger.Log("Conference page " + job.PageUrl.LocalPath + " returned 404", Durandal.Common.Logger.LogLevel.Wrn);
                return returnVal;
            }

            await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.TagName("platform-header"))), TimeSpan.FromSeconds(5));

            // wait for talk list to load (we may not have to actually wait for this)
            var listTile = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.ClassName("list-tile"))), TimeSpan.FromSeconds(5));

            if (listTile == null)
            {
                logger.Log("No valid page data for this conference", Durandal.Common.Logger.LogLevel.Err);
                return returnVal;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            Conference conferenceInfo = new Conference(job.Phase, job.Year);

            // Parse every talk on the page
            int currentSession = 0;
            int currentTalk = 0;
            Regex sessionSeparatorMatcher = new Regex("<div class=\".*?label.*?\"><p class=\"title\">");
            Regex htmlRipper = new Regex("<[^\\s].*?>");
            Regex listItemSeparator = new Regex("<li>[\\w\\W]+?<\\/li>");
            Regex talkInfoExtractor = new Regex("<a href=\"(\\/study.+?)\\\"[\\w\\W]+?class=\\\"primaryMeta\\\".*?>([\\w\\W]+?)<[\\w\\W]+?class=\\\"title\\\">([\\w\\W]+?)</p");
            Regex talkInternalNameExtractor = new Regex(".+/(.+?)\\?lang=");
            Regex talkThumbnailExtractor = new Regex("<img ");

            //logger.Log(webDriver.PageSource);
            foreach (Match listItemBlock in listItemSeparator.Matches(webDriver.PageSource))
            {
                string listItemContents = listItemBlock.Value;

                if (sessionSeparatorMatcher.Match(listItemContents).Success)
                {
                    currentSession++;
                    currentTalk = 0;
                    continue;
                }

                Match talkInfoMatch = talkInfoExtractor.Match(listItemContents);
                if (!talkInfoMatch.Success)
                {
                    logger.Log("Couldn't match talk info on list item", Durandal.Common.Logger.LogLevel.Wrn);
                    logger.Log(listItemContents, Durandal.Common.Logger.LogLevel.Wrn);
                    continue;
                }

                bool hasThumbnail = talkThumbnailExtractor.Match(listItemContents).Success;
                string relativeUrl = talkInfoMatch.Groups[1].Value;
                string speaker = WebUtility.HtmlDecode(talkInfoMatch.Groups[2].Value);
                string title = WebUtility.HtmlDecode(talkInfoMatch.Groups[3].Value);
                string internalName = WebUtility.UrlDecode(StringUtils.RegexRip(talkInternalNameExtractor, relativeUrl, 1));
                title = StringUtils.RegexRemove(htmlRipper, title).Trim();
                speaker = StringUtils.RegexRemove(htmlRipper, speaker).Trim();

                if (string.Equals("Video", title, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("Video:", title, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("Video", speaker, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("Video:", speaker, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping video presentation");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(speaker))
                {
                    logger.Log($"Couldn't match speaker or title completely; assuming is a special video or something: \"{title} - {speaker}\"", Durandal.Common.Logger.LogLevel.Wrn);
                    continue;
                }

                returnVal.Add(new ConferenceTalk()
                {
                    Conference = conferenceInfo,
                    Language = job.Language,
                    PageUrl = new Uri("https://www.churchofjesuschrist.org" + relativeUrl),
                    Speaker = speaker,
                    Title = title,
                    SessionIndex = currentSession,
                    TalkIndex = currentTalk,
                    InternalName = internalName,
                    HasVideoThumbnail = hasThumbnail,
                });

                currentTalk++;
            }

            return returnVal;
        }

        private static async Task<LocalizedStrings> FigureOutStringsForLocale(
            LanguageCode language,
            ChromeDriver webDriver,
            ILogger logger,
            RequestInterceptingHttpProxy httpProxy)
        {
            LocalizedStrings returnVal = new LocalizedStrings();
            logger.Log("Figuring out how to localize " + language.ToBcp47Alpha3String());
            httpProxy.ClearRequestCache();
            string octoberSampleUrl = string.Format("https://www.churchofjesuschrist.org/study/general-conference/2023/10?lang={0}", language.ToBcp47Alpha3String());
            string aprilSampleUrl = string.Format("https://www.churchofjesuschrist.org/study/general-conference/2023/04?lang={0}", language.ToBcp47Alpha3String());
            webDriver.Navigate().GoToUrl(new Uri(octoberSampleUrl));

            await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.TagName("platform-header"))), TimeSpan.FromSeconds(5));

            // For really dumb reasons we have to dig SUPER DEEP and parse the raw response payloads of every proxied response just
            // to find basic page elements
            Regex yearMatcher = new Regex("\\d{4}");
            Regex conferenceStringMatcher = new Regex("<span class=\\\"backToLink.+?\\\">([^<]+)");
            foreach (var interceptedRequest in httpProxy.OutgoingRequests)
            {
                if (interceptedRequest.ResponseData == null || interceptedRequest.ResponseData.Length == 0)
                {
                    continue;
                }

                try
                {
                    string payload = Encoding.UTF8.GetString(interceptedRequest.ResponseData);

                    Match match = conferenceStringMatcher.Match(payload);
                    if (match.Success)
                    {
                        returnVal.ConferenceAlbumName = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                        if (string.Equals("Library", returnVal.ConferenceAlbumName))
                        {
                            returnVal.ConferenceAlbumName = "General Conference";
                        }
                    }

                    if (payload.StartsWith("{"))
                    {
                        JObject json = JObject.Parse(payload);
                        if (json["footer"] != null &&
                            json["footer"]["copyright"] != null)
                        {
                            returnVal.CopyrightTextPattern = StringUtils.RegexReplace(yearMatcher, json["footer"]["copyright"].Value<string>(), "{0}").Trim();
                        }

                        if (json["logo"] != null &&
                            json["logo"]["alt"] != null)
                        {
                            returnVal.NameOfTheChurch = json["logo"]["alt"].Value<string>().Trim();
                        }
                    }
                }
                catch (Exception) { }
            }

            // wait for talk list to load (we may not have to actually wait for this)
            var listTile = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.ClassName("list-tile"))), TimeSpan.FromSeconds(5));

            if (listTile == null)
            {
                logger.Log("No valid page data for this conference", Durandal.Common.Logger.LogLevel.Err);
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            Regex conferenceHeaderMatcher = new Regex("<h1 class=\\\"title\\\">(.+?)<\\/h1>");
            //Regex conferenceHeaderMatcher = new Regex("<title>(.+?)<\\/title>");
            returnVal.OctoberConferenceNamePattern = 
                StringUtils.RegexReplace(yearMatcher,
                    WebUtility.HtmlDecode(
                        StringUtils.RegexRip(conferenceHeaderMatcher, webDriver.PageSource, 1)), "{0}").Trim();

            webDriver.Navigate().GoToUrl(new Uri(aprilSampleUrl));

            await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.TagName("platform-header"))), TimeSpan.FromSeconds(5));
            returnVal.AprilConferenceNamePattern =
                StringUtils.RegexReplace(yearMatcher,
                    WebUtility.HtmlDecode(
                        StringUtils.RegexRip(conferenceHeaderMatcher, webDriver.PageSource, 1)), "{0}").Trim();

            if (string.IsNullOrWhiteSpace(returnVal.NameOfTheChurch))
            {
                logger.Log("Could not parse name of the church in locale " + language.ToBcp47Alpha3String(), Durandal.Common.Logger.LogLevel.Err);
                return null;
            }
            else if (string.IsNullOrWhiteSpace(returnVal.CopyrightTextPattern))
            {
                logger.Log("Could not parse copyright text in locale " + language.ToBcp47Alpha3String(), Durandal.Common.Logger.LogLevel.Err);
                return null;
            }
            else if (string.IsNullOrWhiteSpace(returnVal.ConferenceAlbumName))
            {
                logger.Log("Could not parse conference name in locale " + language.ToBcp47Alpha3String(), Durandal.Common.Logger.LogLevel.Err);
                return null;
            }
            else if (string.IsNullOrWhiteSpace(returnVal.AprilConferenceNamePattern) ||
                string.IsNullOrWhiteSpace(returnVal.OctoberConferenceNamePattern))
            {
                logger.Log("Could not parse conference name pattern in locale " + language.ToBcp47Alpha3String(), Durandal.Common.Logger.LogLevel.Err);
                return null;
            }

            logger.Log(JsonConvert.SerializeObject(returnVal, Formatting.Indented));
            return returnVal;
        }

        private static async Task<string> GetDownloadLink(ConferenceTalk talk, ChromeDriver webDriver, ILogger logger, RequestInterceptingHttpProxy httpProxy)
        {
            int delayMs = 500;
            int retriesLeft = 3;
            while (retriesLeft-- > 0)
            {
                delayMs *= 2;
                webDriver.Navigate().GoToUrl(talk.PageUrl);

                try
                {
                    // See if there is body content for this talk
                    // This can be null for languages like Apache that have a video but no transcription
                    //IWebElement bodyText = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.ClassName("body-block"))), TimeSpan.FromSeconds(5));
                    //if (bodyText == null || bodyText.Text.Length < 1000)
                    //{
                    //    logger.Log("Talk body text does not exist or is too short");
                    //    return null;
                    //}

                    // See if there's a video player at all
                    // (it must be within the header of the talk, sometimes there are inline videos that we want to omit for example in /study/general-conference/2021/10/47nelson?lang=eng)
                    //IWebElement videoContainer = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.CssSelector("header [class~=\"bitmovinplayer-container\"]"))), TimeSpan.FromSeconds(5));
                    IWebElement videoContainer = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.CssSelector("div [class~=\"video\"]"))), TimeSpan.FromSeconds(5));

                    if (videoContainer == null)
                    {
                        logger.Log("No video player on this page.");
                        return null;
                    }

                    // Wait for big video player to load
                    //IWebElement bigPlayButton = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.CssSelector("header [class~=\"bmpui-ui-hugeplaybacktogglebutton\"]"))), TimeSpan.FromSeconds(5));
                    IWebElement bigPlayButton = await WaitForElementToLoad(() => Task.FromResult(webDriver.FindElement(By.CssSelector("div [class~=\"video\"] button"))), TimeSpan.FromSeconds(5));

                    if (bigPlayButton == null)
                    {
                        continue;
                    }

                    await Task.Delay(delayMs);

                    // When we hit the playback button for the first time, the internal elements will appear. Wait for those to load
                    // That will confirm that we've actually hit play.
                    IWebElement playPauseButton = await WaitForElementToLoad(async () =>
                    {
                        bigPlayButton.Click();
                        await Task.Delay(delayMs);
                        IWebElement returnVal = webDriver.FindElement(By.ClassName("bmpui-ui-playbacktogglebutton"));
                        if (returnVal != null && !returnVal.Displayed)
                        {
                            return null;
                        }

                        return returnVal;
                    }, TimeSpan.FromSeconds(5));

                    if (playPauseButton == null)
                    {
                        continue;
                    }

                    // Pause the video playback
                    playPauseButton.Click();

                    // See if we intercepted the master playlist fetch
                    // This is preferable as we can only select the audio transport stream and skip downloading video
                    InterceptedHttpRequest m3uRequest = httpProxy.OutgoingRequests.Where((req) => req.RequestPath.Contains("master.m3u8")).FirstOrDefault();
                    if (m3uRequest != null)
                    {
                        string videoDownloadLink = m3uRequest.Host + m3uRequest.RequestPath;
                        bool first = true;
                        if (m3uRequest.GetParameters.KeyCount > 0)
                        {
                            videoDownloadLink += "?";
                            foreach (var getParam in m3uRequest.GetParameters)
                            {
                                foreach (string getParamValue in getParam.Value)
                                {
                                    videoDownloadLink = videoDownloadLink + (first ? "?" : "&") + WebUtility.UrlEncode(getParam.Key) + "=" + WebUtility.UrlEncode(getParamValue);
                                    first = false;
                                }
                            }
                        }

                        return videoDownloadLink;
                    }
                    else
                    {
                        // Fallback path if we can't find the m3u8 playlist.
                        // Dig into page source to find the video download links to download the full video.
                        Regex downloadLinkMatcher = new Regex("href=\"([^\\\"]+?-360p-[^\\\"]+?\\.mp4\\?download=true)\\\"");
                        string videoDownloadLink = StringUtils.RegexRip(downloadLinkMatcher, webDriver.PageSource, 1, logger);

                        if (string.IsNullOrWhiteSpace(videoDownloadLink))
                        {
                            continue;
                        }

                        return videoDownloadLink;
                    }
                }
                catch (Exception e)
                {
                    // can happen if a button is not clickable or something
                    logger.Log(e, Durandal.Common.Logger.LogLevel.Wrn);
                }
            }

            return null;
        }

        private static async Task<IWebElement> WaitForElementToLoad(Func<Task<IWebElement>> loadFunction, TimeSpan maxWaitTime)
        {
            TimeSpan timeWaited = TimeSpan.Zero;
            TimeSpan backoffTime = TimeSpan.FromMilliseconds(100);
            // Retry loop
            for (int c = 0; c < 100 && timeWaited < maxWaitTime; c++)
            {
                try
                {
                    IWebElement returnVal = await loadFunction();
                    if (returnVal != null)
                    {
                        return returnVal;
                    }
                }
                catch (Exception) { }

                await Task.Delay(backoffTime).ConfigureAwait(false);
                timeWaited += backoffTime;
                backoffTime += TimeSpan.FromMilliseconds(100);
            }

            return null;
        }
    }
}
