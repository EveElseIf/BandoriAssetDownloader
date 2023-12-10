using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    internal static class Helper
    {
        public static string BuildAssetPath(this string versionCode, string asset)
            => $"https://d2ktlshvcuasnf.cloudfront.net/Release/{versionCode}/Android/{asset}";
    }
}
