using System;
using System.IO;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace ItWasMe.PuppeteerBrowserBasics
{
    public static class PuppeteerHelper
    {
        public static async Task<string> GetChromiumPath(Platform platform)
        {
            var env = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            } 
             
            var downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomChromium");
             
            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);
             
            var browserFetcherOptions = new BrowserFetcherOptions { Path = downloadPath, Platform = platform };
            var browserFetcher = new BrowserFetcher(browserFetcherOptions);
             
            await browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
             
            var chromiumExecutionPath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultRevision);
             
            if (string.IsNullOrEmpty(chromiumExecutionPath))
            {
                throw new Exception("failed to load the browser");
            }

            return chromiumExecutionPath; 
        }
    }
}
