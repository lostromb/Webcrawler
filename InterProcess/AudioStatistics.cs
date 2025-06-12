using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.InterProcess
{
    public class AudioStatistics
    {
        public MovingPercentile VolumeHistogram;
        public TimeSpan AudioStartTime;
        public TimeSpan AudioEndTime;
    }
}
