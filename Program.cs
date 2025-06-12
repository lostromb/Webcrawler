using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Compression.Zip;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Language;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebCrawler.Scenarios;
using WebCrawler.Scenarios.GeneralConf;

namespace WebCrawler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //VolumeTest().Await();
            //TwoKinds.Download().Await();
            //Tamberlane.Download().Await();
            //MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
            //    new MaxFunShowDetails()
            //    {
            //        InternalId = "wonderful",
            //        FileNameBase = "Wonderful",
            //        MetadataAlbum = "Wonderful!",
            //        MetadataGenre = "Speech",
            //        MetadataArtist = "Griffin and Rachel McElroy",
            //        MetadataAlbumArtist = "Wonderful!"
            //    },
            //    new DirectoryInfo(@"S:\Audio\Podcasts\Wonderful"),
            //    0).Await();

            //MangadexDownloader.DownloadManga().Await();
            //TheChosen.Download().Await();
            //NinetyNineInvisible.DownloadPodcast().Await();
            //ConferenceDownloaderChromium.DownloadConference().Await();

            ScheduledWebCrawler.GetLatestMaxFunPodcasts().Await();

            //PodBean.DownloadPodcast(
            //    new PodBeanDetails()
            //    {
            //        InternalId = "unshaken",
            //        FileNameBase = "Unshaken Saints",
            //        MetadataAlbum = "Unshaken Saints",
            //        MetadataGenre = "Speech",
            //        MetadataArtist = "Jared Halverson",
            //        MetadataAlbumArtist = "Unshaken Saints"
            //    },
            //    new DirectoryInfo(@"S:\Audio\Podcasts\Unshaken Saints\2024 - The Book of Mormon")).Await();

            //TalkProcessor.DownloadTalkAndConvert(
            //    new ConferenceTalk()
            //    {
            //        Speaker = "Gary E. Stevenson",
            //        Title = "The Greatest Easter Story Ever Told",
            //        Language = LanguageCode.ENGLISH,
            //        SessionIndex = 1,
            //        TalkIndex = 0,
            //        Conference = new Conference(ConferencePhase.April, 2023),
            //    },
            //    new WebClient(),
            //    "https://manifest.prod.boltdns.net/manifest/v1/hls/v4/clear/1241706627001/821b6c35-31e9-4c78-aa82-7e5bba6b4535/10s/master.m3u8?fastly_token=NjYxMzQ2ZDJfZWZmYTA3YTg3MjYyZTM4ZDVlMTU1OWE3ZWNkYjRjNDEyNzVhNzRmYjU0MjZmMzM4MzgxZDE3NGM2ZDE5Y2QxMw%3D%3D",
            //    new DirectoryInfo(@"C:\Code\WebCrawler\bin\staging"),
            //    new ConsoleLogger()).Await();
        }
    }
}
