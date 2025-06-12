using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCrawler.Scenarios.GeneralConf
{
    public class InterceptedHttpRequest
    {
        public string Host { get; set; }
        public string RequestPath { get; set; }
        public IHttpFormParameters GetParameters { get; set; }
        public int ResponseCode { get; set; }
        public byte[] RequestData { get; set; }
        public byte[] ResponseData { get; set; }

        public override int GetHashCode()
        {
            return Host.GetHashCode() +
                RequestPath.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Host}{RequestPath}";
        }
    }
}
