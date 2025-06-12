namespace Durandal.Plugins.Weather
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Durandal.Common.IO;
    using Common.File;
    using System.Threading.Tasks;

    public class WeatherBackgroundGenerator
    {
        private readonly IFileSystem _fileSystem;
        private readonly VirtualPath _bgImageDirectory;
        private readonly IList<WeatherBackgroundImage> _knownImages;

        private WeatherBackgroundGenerator(IFileSystem fileSystem, VirtualPath backgroundImageDirectory)
        {
            _fileSystem = fileSystem;
            _bgImageDirectory = backgroundImageDirectory;
            _knownImages = new List<WeatherBackgroundImage>();
        }

        private async Task Initialize()
        {
            foreach (VirtualPath f in await _fileSystem.ListFilesAsync(_bgImageDirectory).ConfigureAwait(false))
            {
                string ext = f.Extension.ToLowerInvariant();
                if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    _knownImages.Add(new WeatherBackgroundImage(f.Name));
                }
            }
        }

        public static async Task<WeatherBackgroundGenerator> Build(IFileSystem fileSystem, VirtualPath backgroundImageDirectory)
        {
            WeatherBackgroundGenerator returnVal = new WeatherBackgroundGenerator(fileSystem, backgroundImageDirectory);
            await returnVal.Initialize().ConfigureAwait(false);
            return returnVal;
        }
        
        public string GetBackgroundImage(WeatherTimeOfDay timeOfDay, WeatherCondition condition)
        {
            // TODO if night time and clear sky, set time of day to "moon"

            float closestDistance = 10.0f;
            List<WeatherBackgroundImage> images = new List<WeatherBackgroundImage>();
            foreach (WeatherBackgroundImage image in _knownImages)
            {
                float dist = image.Condition.Difference(condition) + (1.5f * image.Time.Difference(timeOfDay));
                if (dist < closestDistance)
                {
                    images.Clear();
                    closestDistance = dist;
                }
                if (dist <= closestDistance)
                {
                    images.Add(image);
                }
            }

            // Pick a random image from the set
            if (images.Count == 0)
            {
                // Shouldn't hit this unless no images exist
                return "day clear.jpg";
            }
            else
            {
                Random rng = new Random();
                int index = rng.Next(0, images.Count);
                return images[index].FileName;
            }
        }

        private class WeatherBackgroundImage
        {
            public string FileName;
            public WeatherTimeOfDay Time;
            public WeatherCondition Condition;

            public WeatherBackgroundImage(string fileName)
            {
                this.FileName = fileName;
                string[] fileNameParts = fileName.Substring(0, fileName.LastIndexOf('.')).Split(' ');
                if (fileNameParts.Length >= 2)
                {
                    this.Time = WeatherTimeOfDayExtensions.Parse(fileNameParts[0]);
                    this.Condition = WeatherConditionExtensions.Parse(fileNameParts[1]);
                }
                else
                {
                    this.Time = WeatherTimeOfDay.Day;
                    this.Condition = WeatherCondition.Clear;
                }
                // Console.WriteLine("Parsed " + fileName + " as " + Time.ToString() + " / " + Condition.ToString());
            }
        }
    }
}
