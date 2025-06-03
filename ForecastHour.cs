using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WeatherCanAm
{
    public class ForecastHour
    {
        public int Number { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Day { get; set; }
        public int Temperature { get; set; }
        public string? ShortForecast { get; set; }
        public int PrecipitationChance { get; set; }
        public string? WindSpeed { get; set; }
        public string? WindDirection { get; set; }

        public static string DisplayTime(string? timeString)
        {
            DateTimeOffset dateTime = DateTimeOffset.Parse(timeString ?? "");
            return dateTime.ToString("htt");
        }

        public static string DisplayDay(string? timeString)
        {
            DateTimeOffset dateTime = DateTimeOffset.Parse(timeString ?? "");
            return dateTime.ToString("dddd, MMMM d, yyyy");
        }

        public string DisplayTemperature()
        {
            return $"{Temperature} F";
        }

        public string DisplaySummary()
        {
            var precipitationStr = "";
            if (PrecipitationChance >= 5)
            {
                precipitationStr = $" w/ {PrecipitationChance}% chance of rain";
            }
            var summary = $"{DisplayTime(StartTime),-5} {DisplayTemperature(),-5} | {ShortForecast}{precipitationStr}";
            return summary;
        }

        public ForecastHour(string? startTime) 
        {
            StartTime = startTime;
            if (!string.IsNullOrWhiteSpace(startTime))
            {
                try
                {
                    DateTimeOffset parsed = DateTimeOffset.Parse(startTime);
                    Day = parsed.ToString("yyyyMMdd");
                }
                catch
                {
                    Day = null;
                }
            }
        }

        public ForecastHour() { }
    }
}
