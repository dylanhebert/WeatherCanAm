﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeatherCanAm
{
    public class Station : Coordinate
    {
        public string? Name { get; set; }
        public int Id { get; set; }
        public string? StationIdentifier { get; set; }
        public string? City { get; set; }
    }
}
