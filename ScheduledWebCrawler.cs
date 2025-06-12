using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebCrawler.Scenarios;

namespace WebCrawler
{
    internal class ScheduledWebCrawler
    {
        public static async Task GetLatestMaxFunPodcasts()
        {
            await MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
                new MaxFunShowDetails()
                {
                    InternalId = "secret-histories-of-nerd-mysteries",
                    FileNameBase = "SHoNM",
                    MetadataAlbum = "Secret Histories of Nerd Mysteries",
                    MetadataGenre = "Speech",
                    MetadataArtist = "Austin Taylor & Brenda Snell",
                    MetadataAlbumArtist = "Secret Histories of Nerd Mysteries"
                },
                new DirectoryInfo(@"S:\Audio\Podcasts\Secret Histories of Nerd Mysteries"),
                117); 
            
            await MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
                new MaxFunShowDetails()
                {
                    InternalId = "secretly-incredibly-fascinating",
                    FileNameBase = "Secretly Incredibly Fascinating",
                    MetadataAlbum = "Secretly Incredibly Fascinating",
                    MetadataGenre = "Speech",
                    MetadataArtist = "Alex Schmidt, Katie Goldin & Special Guests",
                    MetadataAlbumArtist = "Secretly Incredibly Fascinating"
                },
                new DirectoryInfo(@"S:\Audio\Podcasts\Secretly Incredibly Fascinating"),
                197,
                new SifFilter());

            await MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
                new MaxFunShowDetails()
                {
                    InternalId = "my-brother-my-brother-and-me",
                    FileNameBase = "MBMBaM",
                    MetadataAlbum = "My Brother, My Brother and Me",
                    MetadataGenre = "Comedy",
                    MetadataArtist = "Justin, Travis and Griffin McElroy",
                    MetadataAlbumArtist = "My Brother, My Brother and Me"
                },
                new DirectoryInfo(@"S:\Audio\Podcasts\My Brother, My Brother and Me"),
                713,
                new MbmbamFilter());

            await MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
                new MaxFunShowDetails()
                {
                    InternalId = "sawbones",
                    FileNameBase = "Sawbones",
                    MetadataAlbum = "Sawbones: A Marital Tour of Misguided Medicine",
                    MetadataGenre = "Speech",
                    MetadataArtist = "Justin and Sydnee McElroy",
                    MetadataAlbumArtist = "Sawbones"
                },
                new DirectoryInfo(@"S:\Audio\Podcasts\Sawbones"),
                498,
                new SawbonesFilter());

            //await MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
            //    new MaxFunShowDetails()
            //    {
            //        InternalId = "adventure-zone",
            //        FileNameBase = "The Adventure Zone",
            //        MetadataAlbum = "The Adventure Zone Versus Dracula",
            //        MetadataGenre = "Comedy",
            //        MetadataArtist = "Justin, Travis, Clint, and Griffin McElroy",
            //        MetadataAlbumArtist = "The Adventure Zone",
            //        EpisodeNumberPadding = 2,
            //        EncodeBitrate = 24
            //    },
            //    new DirectoryInfo(@"S:\Audio\Podcasts\The Adventure Zone\The Adventure Zone Versus Dracula"),
            //    0,
            //    new DraculaFilter());

            MaxFunPodcasts.DownloadNewMaxFunPodcastEpisodes(
                new MaxFunShowDetails()
                {
                    InternalId = "wonderful",
                    FileNameBase = "Wonderful!",
                    MetadataAlbum = "Wonderful!",
                    MetadataGenre = "Speech",
                    MetadataArtist = "Griffin and Rachel McElroy",
                    MetadataAlbumArtist = "Wonderful!"
                },
                new DirectoryInfo(@"S:\Audio\Podcasts\Wonderful"),
                354,
                new WonderfulFilter()).Await();

            //PodBean.DownloadPodcast(
            //    new PodBeanDetails()
            //    {
            //        InternalId = "unshaken",
            //        FileNameBase = "Unshaken Saints",
            //        MetadataAlbum = "2024 - The Book of Mormon",
            //        MetadataGenre = "Speech",
            //        MetadataArtist = "Jared Halverson",
            //        MetadataAlbumArtist = "Unshaken Saints"
            //    },
            //    new DirectoryInfo(@"S:\Audio\Podcasts\Unshaken Saints\2024 - The Book of Mormon")).Await();
        }

        public class MbmbamFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^MBMBaM \\d+:\\s+");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                return true;
            }
        }

        public class SawbonesFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^Sawbones:\\s+");
            private Regex prefixRemover2 = new Regex("^Sawbones \\d+:\\s+");
            private Regex prefixRemover3 = new Regex("^Sawbones [\\-‒–—]\\s+");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                episodeTitle = StringUtils.RegexRemove(prefixRemover2, episodeTitle);
                episodeTitle = StringUtils.RegexRemove(prefixRemover3, episodeTitle);
                return true;
            }
        }

        public class OutreSpaceFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^The Adventure Zone: Outre Space [\\-‒–—]\\s+");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                if (!episodeTitle.AsSpan().Contains("Dracula".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping non-dracula episode " + episodeTitle);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                return true;
            }
        }

        public class DraculaFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^The Adventure Zone Versus Dracula [\\-‒–—]\\s+");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                if (!episodeTitle.AsSpan().Contains("Dracula".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping non-dracula episode " + episodeTitle);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                return true;
            }
        }

        public class SifFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^Secretly Incredibly Fascinating:\\s+");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                return true;
            }
        }

        public class WonderfulFilter : MaxFunEpisodeFilter
        {
            private Regex prefixRemover1 = new Regex("^Wonderful!\\s+\\d+:\\s*");

            public bool Process(ref string episodeTitle, ILogger logger)
            {
                if (episodeTitle.StartsWith("TRANSCRIPT", StringComparison.OrdinalIgnoreCase) ||
                    episodeTitle.AsSpan().Contains("(Transcript)".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    logger.Log("Skipping transcript " + episodeTitle, LogLevel.Std);
                    return false;
                }

                episodeTitle = StringUtils.RegexRemove(prefixRemover1, episodeTitle);
                return true;
            }
        }
    }
}
