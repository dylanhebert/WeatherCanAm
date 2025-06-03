using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text.Json;
using WeatherCanAm;

class Program
{
    static List<string> StateCodes = new()
    {
        "AL", "AK", "AS", "AR", "AZ", "CA", "CO", "CT", "DE", "DC",
        "FL", "GA", "GU", "HI", "ID", "IL", "IN", "IA", "KS", "KY",
        "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE",
        "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR",
        "PA", "PR", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VI",
        "VA", "WA", "WV", "WI", "WY"
    };

    static async Task Main(string[] args)
    {
        Console.WriteLine("Welcome to the weather checker!");

        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DylanWeatherCanAm");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        while (true)
        {
            Coordinate? coordinate = null;
            while (coordinate is null)
            {
                Console.WriteLine("\n\nEnter '1' to find a specific weather station\nEnter '2' to manually enter coordinates\nEnter 'exit' to leave");
                var input = Console.ReadLine() ?? "";

                // User chooses how they want to get the coordinates
                if (input.Equals("1"))
                {
                    coordinate = await FindStationCoordinates(client);
                }
                else if (input.Equals("2"))
                {
                    coordinate = GetManualCoordinates();
                }
                else if (input.Equals("exit"))
                {
                    return;
                }
                else
                {
                    Console.WriteLine("Invalid option!");
                }
            }

            var forecast = await GetForecast(client, coordinate);
            Console.WriteLine($"{forecast}");
        }
    }

    static async Task<Coordinate?> FindStationCoordinates(HttpClient client)
    {
        // User picks state
        var validState = false;
        var inputState = "";
        while (!validState)
        {
            Console.WriteLine("\nType a U.S. state code/abbreviation (e.g. 'TX') to view the state's zones. Type 'exit' to go back");
            inputState = Console.ReadLine() ?? "";

            if (inputState.Equals("exit"))
            {
                return null;
            }

            inputState = inputState.Trim().ToUpper();
            if (!StateCodes.Contains(inputState))
            {
                Console.WriteLine("Invalid state code/abbreviation!");
            }
            else
            {
                validState = true;
            }
        }

        // Get Public Zones in state (only public zones have stations assigned)
        var zonesUrl = $"https://api.weather.gov/zones?area={inputState}&type=public";
        var zones = new List<Zone>();
        try
        {
            Console.WriteLine($"\nGetting all zones for {inputState}...");
            var content = await GetAsyncJsonContent(client, zonesUrl);
            using var jsonDoc = JsonDocument.Parse(content);
            var features = jsonDoc.RootElement.GetProperty("features");

            foreach (var feature in features.EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                var zone = new Zone()
                {
                    Id = zones.Count(),
                    Name = properties.GetProperty("name").GetString(),
                    ZoneId = properties.GetProperty("id").GetString(),
                };
                zones.Add(zone);

                Console.WriteLine($"{zone.ZoneId} - {zone.Name}");
            }

            Console.WriteLine($"Found {zones.Count()} public zones in {inputState}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.ToString()}");
            return null;
        }

        // User picks zone
        Zone? selectedZone = null;
        while (selectedZone == null)
        {
            Console.WriteLine($"\nType a zone's name OR zone's ID in {inputState}:");
            var inputZone = Console.ReadLine() ?? "";
            selectedZone = zones.FirstOrDefault(x => x.Name?.ToLower() == inputZone.ToLower());
            if (selectedZone == null)
            {
                selectedZone = zones.FirstOrDefault(x => x.ZoneId?.ToLower() == inputZone.ToLower());
                if (selectedZone == null)
                {
                    Console.WriteLine("Invalid zone name or ID!");
                }
            }
        }

        // Get Stations in Zone
        var zoneName = selectedZone.Name ?? "";
        var stations = new List<Station>();
        try
        {
            Console.WriteLine($"\nGetting all stations associated with {zoneName}...");
            var stationsUrl = $"https://api.weather.gov/zones/forecast/{selectedZone.ZoneId}/stations";
            var content = await GetAsyncJsonContent(client, stationsUrl);
            using var jsonDoc = JsonDocument.Parse(content);
            var features = jsonDoc.RootElement.GetProperty("features");

            foreach (var feature in features.EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                var coordinates = feature.GetProperty("geometry").GetProperty("coordinates").EnumerateArray().ToArray();
                var station = new Station()
                {
                    Id = stations.Count(),
                    Name = properties.GetProperty("name").GetString(),
                    StationIdentifier = properties.GetProperty("stationIdentifier").GetString(),
                    Latitude = coordinates[1].GetDecimal(),
                    Longitude = coordinates[0].GetDecimal()
                };
                stations.Add(station);
                Console.WriteLine($"{station.StationIdentifier} - {station.Name}");
            }

            Console.WriteLine($"Found {stations.Count()} stations associated with {zoneName}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.ToString()}");
            return null;
        }

        // User picks station
        Station? selectedStation = null;
        while (selectedStation == null)
        {
            Console.WriteLine($"\nType a station's ID to view the forecast:");
            var inputStation = Console.ReadLine() ?? "";
            selectedStation = stations.FirstOrDefault(x => x.StationIdentifier?.ToLower() == inputStation.ToLower());
            if (selectedStation == null)
            {
                Console.WriteLine("Invalid station ID!");
            }
        }

        return selectedStation as Coordinate;
    }

    static async Task<string> GetAsyncJsonContent(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    static async Task<string> GetForecast(HttpClient client, Coordinate coordinate)
    {
        Console.WriteLine($"\nGetting forecast info for [{coordinate.Latitude}, {coordinate.Longitude}]...");
        var forecastUrl = $"https://api.weather.gov/points/{coordinate.Latitude},{coordinate.Longitude}";
        try
        {
            var content = await GetAsyncJsonContent(client, forecastUrl);
            using var jsonDoc = JsonDocument.Parse(content);
            var hourlyUrl = jsonDoc.RootElement.GetProperty("properties").GetProperty("forecastHourly").GetString();

            if (hourlyUrl is null)
            {
                return "Error: Could not get hourly forecast.";
            }

            var hourlyForecast = await BuildHourlyForecast(client, hourlyUrl);
            return hourlyForecast;
        }
        catch (Exception ex)
        {
            return "Error: Could not retrieve forecast.";
        }
    }

    static async Task<string> BuildHourlyForecast(HttpClient client, string hourlyUrl)
    {
        var content = await GetAsyncJsonContent(client, hourlyUrl);
        using var jsonDoc = JsonDocument.Parse(content);
        var hourElements = jsonDoc.RootElement.GetProperty("properties").GetProperty("periods");
        var forecastHours = new List<ForecastHour>();

        var fullString = "";
        var currentDay = "";
        foreach (var hour in hourElements.EnumerateArray())
        {
            var forecastHour = new ForecastHour(hour.GetProperty("startTime").GetString())
            {
                Number = hour.GetProperty("number").GetInt32(),
                EndTime = hour.GetProperty("endTime").GetString(),
                Temperature = hour.GetProperty("temperature").GetInt32(),
                ShortForecast = hour.GetProperty("shortForecast").GetString(),
                PrecipitationChance = hour.GetProperty("probabilityOfPrecipitation").GetProperty("value").GetInt32()
            };
            forecastHours.Add(forecastHour);

            // Visually separate hours by each day
            if(currentDay != forecastHour.Day)
            {
                currentDay = forecastHour.Day;
                fullString = $"{fullString}\n\n---------- {ForecastHour.DisplayDay(forecastHour.StartTime)} ----------";
            }

            // Add hour and all info to string
            fullString = $"{fullString}\n   {forecastHour.DisplaySummary()}";
        }

        return fullString;
    }

    static Coordinate? GetManualCoordinates()
    {
        decimal latitude = 0;
        decimal longitude = 0;
        string latitudeStr = string.Empty;
        string longitudeStr = string.Empty;

        while (string.IsNullOrEmpty(latitudeStr) || latitude == 0)
        {
            Console.WriteLine("Enter latitude:");
            latitudeStr = Console.ReadLine() ?? "";

            decimal.TryParse(latitudeStr, out latitude);

            if (string.IsNullOrEmpty(latitudeStr) || latitude == 0)
            {
                Console.WriteLine("Invalid latitude! Try again.");
            }
        }

        while (string.IsNullOrEmpty(longitudeStr) || longitude == 0)
        {
            Console.WriteLine("Enter longitude:");
            longitudeStr = Console.ReadLine() ?? "";

            decimal.TryParse(longitudeStr, out longitude);

            if (string.IsNullOrEmpty(longitudeStr) || longitude == 0)
            {
                Console.WriteLine("Invalid longitude! Try again.");
            }
        }

        return new Coordinate()
        {
            Latitude = latitude,
            Longitude = longitude
        };
    }
}