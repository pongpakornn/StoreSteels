using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace StoreSteels.Helpers
{
    public static class ImageCacheHelper
    {
        private static readonly Dictionary<string, BitmapImage> _cache
            = new();

        public static BitmapImage LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_cache.ContainsKey(path))
                return _cache[path];

            try
            {
                BitmapImage bitmap = new();

                bitmap.BeginInit();

                bitmap.UriSource = new Uri(path);

                // ✅ ลด Memory
                bitmap.DecodePixelWidth = 120;

                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                bitmap.EndInit();

                bitmap.Freeze();

                _cache[path] = bitmap;

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}