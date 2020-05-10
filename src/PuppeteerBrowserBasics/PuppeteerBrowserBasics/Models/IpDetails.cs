using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ItWasMe.PuppeteerBrowserBasics.Models
{
    public class IpDetails
    {
        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; }
        [JsonProperty("continentCode")]
        public string ContinentCode { get; set; }
        [JsonProperty("continentName")]
        public string ContinentName { get; set; }
        [JsonProperty("countryCode")]
        public string CountryCode { get; set; }
        [JsonProperty("countryName")]
        public string CountryName { get; set; }
        [JsonProperty("stateProv")]
        public string State { get; set; }
        [JsonProperty("city")]
        public string City { get; set; }
    }
}
