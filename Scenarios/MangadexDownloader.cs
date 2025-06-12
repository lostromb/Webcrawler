using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebCrawler.Mangadex.Api;
using WebCrawler.Mangadex.Converters;
using WebCrawler.Mangadex.Schemas;

namespace WebCrawler.Scenarios
{
    public static class MangadexDownloader
    {
        public static async Task DownloadManga()
        {
            ILogger logger = new ConsoleLogger("WebCrawler", Durandal.Common.Logger.LogLevel.Std | Durandal.Common.Logger.LogLevel.Wrn | Durandal.Common.Logger.LogLevel.Err);

            string downloadTargetDirectory = Environment.CurrentDirectory + "\\staging";
            if (!Directory.Exists(downloadTargetDirectory))
            {
                logger.Log("Creating staging directory " + downloadTargetDirectory);
                Directory.CreateDirectory(downloadTargetDirectory);
            }

            IFileSystem stagingFileSystem = new RealFileSystem(logger.Clone("FileSystem"), downloadTargetDirectory);

            //PooledTcpClientSocketFactory socketFactory = new PooledTcpClientSocketFactory(logger.Clone("SocketFactory"), NullMetricCollector.Singleton, DimensionSet.Empty,
            //    System.Security.Authentication.SslProtocols.None, true);
            //IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(
            //    new WeakPointer<ISocketFactory>(socketFactory),
            //    new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
            //    DimensionSet.Empty);

            IHttpClientFactory httpClientFactory = new PortableHttpClientFactory(
                new WeakPointer<IMetricCollector>(NullMetricCollector.Singleton),
                DimensionSet.Empty);

            //string mangaName = "Please Look After the Dragon";
            //Guid mangaId = Guid.Parse("5924767b-2288-40c7-8304-0d5879c353b3");
            string mangaName = "Robopon";
            Guid mangaId = Guid.Parse("eb2454df-4c6f-4bad-ab92-58848e8fb90f");
            LanguageCode desiredLanguage = LanguageCode.Parse("en");
            IList<decimal> chaptersToFetch = Enumerable.Range(1, 3).Select((x) => (decimal)x).ToList();

            MangadexApi api = new MangadexApi(httpClientFactory, logger.Clone("MangaApi"));
            logger.Log("Inspecting manga ID " + mangaId);

            // Step 1 - find out what scanlation groups are prominent for the given manga and rank them
            // This will give us an idea of what group to prioritize in order to try and keep
            // translations consistent across chapters.
            logger.Log("Getting feed info for " + mangaId);
            FeedQueryResponse overallFeedResponse = await api.GetFeed(
                new FeedQueryParameters()
                {
                    Offset = 0,
                    Limit = 96,
                    MangaId = mangaId,
                    TranslatedLanguage = new List<LanguageCode>()
                    {
                        desiredLanguage
                    },
                },
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

            Counter<Guid> scanlationGroupCounts = GetScanlationGroupCounts(overallFeedResponse);

            // Now get the list of all chapters of the given manga

            logger.Log("Getting chapter list for " + mangaName);
            AggregateQueryResponse aggregateInfo = await api.GetAggregate(
                new AggregateQueryParameters()
                {
                    MangaId = mangaId,
                    TranslatedLanguage = new List<LanguageCode>()
                    {
                        desiredLanguage
                    },
                },
                CancellationToken.None,
                DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

            // Download all
            chaptersToFetch.Clear();
            foreach (var volume in aggregateInfo.Volumes.Values)
            {
                foreach (var chapter in volume.Chapters)
                {
                    decimal val;
                    if (decimal.TryParse(chapter.Key, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out val))
                    {
                        chaptersToFetch.Add(val);
                    }
                    else
                    {
                        logger.Log($"Cannot parse chapter {chapter.Key} in volume {volume.Volume}", Durandal.Common.Logger.LogLevel.Wrn);
                    }
                }
            }

            foreach (decimal chapterToDownload in chaptersToFetch)
            {
                logger.Log("Determining available versions of chapter " + chapterToDownload + " of " + mangaName);
                List<Guid> translationsOfThisChapter = new List<Guid>();
                foreach (var volume in aggregateInfo.Volumes)
                {
                    foreach (var chapter in volume.Value.Chapters)
                    {
                        decimal chapterId;
                        if (decimal.TryParse(chapter.Key, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out chapterId) &&
                            chapterId == chapterToDownload)
                        {
                            translationsOfThisChapter.Add(chapter.Value.Id);
                            if (chapter.Value.Others != null)
                            {
                                translationsOfThisChapter.AddRange(chapter.Value.Others);
                            }
                        }
                    }
                }

                if (translationsOfThisChapter.Count > 0)
                {
                    // Now fetch the actual details for the chapter.
                    // This maps scanlation group ID -> chapter info
                    IDictionary<Guid, ChapterEntity> chapterMapping = new Dictionary<Guid, ChapterEntity>();

                    foreach (Guid chapterTranslation in translationsOfThisChapter)
                    {
                        ChapterQueryResponse chapterResponse = await api.GetChapterInfo(chapterTranslation, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                        MangadexEntity scanlationGroupEntity = chapterResponse.Data.Relationships.Where((c) => c.Type == MangadexEntityType.ScanlationGroup).FirstOrDefault();
                        if (scanlationGroupEntity != null)
                        {
                            chapterMapping[scanlationGroupEntity.Id] = chapterResponse.Data;
                        }
                        else
                        {
                            // Unknown group
                            chapterMapping[Guid.Empty] = chapterResponse.Data;
                        }
                    }

                    // Find the chapter version that matches the highest-ranked translation group
                    bool foundChapter = false;
                    foreach (Guid preferredScanlationGroup in scanlationGroupCounts.OrderByDescending((c) => c.Value).Select((c) => c.Key))
                    {
                        if (foundChapter)
                        {
                            break;
                        }

                        if (chapterMapping.ContainsKey(preferredScanlationGroup))
                        {
                            logger.Log("Downloading chapter " + chapterToDownload + " of " + mangaName);
                            await DownloadFullChapter(
                                chapterMapping[preferredScanlationGroup],
                                mangaName,
                                api,
                                stagingFileSystem,
                                stagingFileSystem,
                                logger,
                                CancellationToken.None,
                                DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                            // We have the full chapter. No need to download other variants.
                            foundChapter = true;
                        }
                    }

                    // We couldn't find any version from any known scanlation group (assuming we had an incomplete sampling of groups).
                    // So just pull whatever chapter is available.
                    if (!foundChapter)
                    {
                        logger.Log("No preferred scanlation group found! Falling back to first available version of the chapter", Durandal.Common.Logger.LogLevel.Wrn);
                        logger.Log("Downloading chapter " + chapterToDownload + " of " + mangaName);
                        await DownloadFullChapter(
                            chapterMapping.First().Value,
                            mangaName,
                            api,
                            stagingFileSystem,
                            stagingFileSystem,
                            logger,
                            CancellationToken.None,
                            DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    }
                }
                else
                {
                    logger.Log("Chapter not found: " + chapterToDownload, Durandal.Common.Logger.LogLevel.Err);
                }
            }
        }

        private static async Task DownloadFullChapter(
            ChapterEntity chapter,
            string mangaName,
            MangadexApi api,
            IFileSystem targetFileSystem,
            IFileSystem stagingFileSystem,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            ChapterImageQueryResponse imageInfo = await api.GetChapterImageInfo(chapter.Id, cancelToken, realTime).ConfigureAwait(false);
            logger.Log("Downloading " + imageInfo.Chapter.Data.Count + " images");
            IList<VirtualPath> downloadedImages = await api.DownloadChapterImages(imageInfo.BaseUrl, imageInfo.Chapter, stagingFileSystem, logger).ConfigureAwait(false);

            if (downloadedImages == null)
            {
                // Download failed.
                logger.Log("Aborting download of chapter " + chapter.Attributes.Chapter, Durandal.Common.Logger.LogLevel.Err);
                return;
            }

            string formattedArchiveName;
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                decimal parsedChapterNum = decimal.Parse(chapter.Attributes.Chapter, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                if (parsedChapterNum != Math.Floor(parsedChapterNum))
                {
                    // It's a weird decimal chapter. Pad with zeroes the best we can
                    pooledSb.Builder.AppendFormat("{0} #", mangaName);
                    int leadingZeroes = 0;
                    if (chapter.Attributes.Chapter.Contains('.'))
                    {
                        leadingZeroes = Math.Max(0, 3 - chapter.Attributes.Chapter.IndexOf('.'));
                    }

                    if (leadingZeroes > 0)
                    {
                        pooledSb.Builder.Append('0', leadingZeroes);
                    }

                    pooledSb.Builder.Append(chapter.Attributes.Chapter);
                }
                else
                {
                    // Normal zero-padded integer with up to 3 digits
                    pooledSb.Builder.AppendFormat("{0} #{1:D3}", mangaName, (int)parsedChapterNum);
                }

                if (!string.IsNullOrWhiteSpace(chapter.Attributes.Title))
                {
                    pooledSb.Builder.AppendFormat(" - {0}", chapter.Attributes.Title);
                }

                foreach (var reservedChar in Path.GetInvalidFileNameChars())
                {
                    pooledSb.Builder.Replace(reservedChar, '_');
                }

                pooledSb.Builder.Append(".cbz");
                formattedArchiveName = pooledSb.Builder.ToString();
            }

            // Zip them into an archive
            logger.Log("Creating archive " + formattedArchiveName);
            VirtualPath archivePath = new VirtualPath(formattedArchiveName);
            if (await stagingFileSystem.ExistsAsync(archivePath))
            {
                logger.Log("Output comic file " + archivePath.FullName + " already exists; overwriting...", Durandal.Common.Logger.LogLevel.Wrn);
                await stagingFileSystem.DeleteAsync(archivePath);
            }

            logger.Log("Compressing " + archivePath.FullName, Durandal.Common.Logger.LogLevel.Vrb);
            using (ZipFile comicArchive = new ZipFile(archivePath, logger.Clone("OutputZipFile"), targetFileSystem))
            {
                comicArchive.CompressionLevel = Durandal.Common.Compression.ZLib.CompressionLevel.BestSpeed;
                comicArchive.AddFiles(downloadedImages);
                comicArchive.Save();
            }

            // Dispose of all intermediate files
            try
            {
                foreach (VirtualPath tempImage in downloadedImages)
                {
                    stagingFileSystem.Delete(tempImage);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, Durandal.Common.Logger.LogLevel.Err);
            }
        }

        private static Counter<Guid> GetScanlationGroupCounts(FeedQueryResponse feedResponse)
        {
            Counter<Guid> scanlationGroupCounter = new Counter<Guid>();
            foreach (var responseObject in feedResponse.Data)
            {
                if (responseObject.Type == MangadexEntityType.Chapter)
                {
                    ChapterEntity chapter = responseObject as ChapterEntity;
                    foreach (var relationship in chapter.Relationships)
                    {
                        if (relationship.Type == MangadexEntityType.ScanlationGroup)
                        {
                            scanlationGroupCounter.Increment(relationship.Id);
                        }
                    }
                }
            }

            return scanlationGroupCounter;
        }

        private struct ChapterAndPage
        {
            public readonly int ChapterNum;
            public readonly int PageNum;

            public ChapterAndPage(int chapter, int page)
            {
                ChapterNum = chapter;
                PageNum = page;
            }

            public override int GetHashCode()
            {
                return ChapterNum.GetHashCode() ^ (PageNum.GetHashCode() << 1);
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is ChapterAndPage))
                {
                    return false;
                }

                ChapterAndPage other = (ChapterAndPage)obj;
                return ChapterNum == other.ChapterNum &&
                    PageNum == other.PageNum;
            }
        }

        private static async Task ExtractPageImageLink(IWebDriver webDriver, ILogger logger, ChapterAndPage currentPage, IDictionary<ChapterAndPage, Uri> outputPages)
        {
            // Retry loop
            for (int c = 0; c < 100; c++)
            {
                foreach (IWebElement webElement in webDriver.FindElements(By.XPath("/html/body/div[@id=\'content\']/div[2]/*/*/img")))
                {
                    logger.Log("Found page " + currentPage.PageNum + " image " + webElement.GetAttribute("src"), Durandal.Common.Logger.LogLevel.Vrb);
                    outputPages[currentPage] = new Uri(webElement.GetAttribute("src"));
                    return;
                }

                await Task.Delay(100);
            }
        }
    }
}
