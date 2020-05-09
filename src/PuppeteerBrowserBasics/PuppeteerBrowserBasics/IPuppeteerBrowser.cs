using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ItWasMe.PuppeteerBrowserBasics.Models;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace ItWasMe.PuppeteerBrowserBasics
{
    public interface IPuppeteerBrowser : IDisposable
    {
        Task ScrollPageSync(Page page, int maxScroll = 40000, int step = 200);
        Task ScrollPageSync(Page page, TimeSpan stepDelay, int maxScroll = 40000, int step = 200);
        Task<string> GetPageContent(string stringUrl, params WaitUntilNavigation[] waitUntilNavigations);
        Task<Page> GetPage(string url, TimeSpan? timeoutPageLoad, params WaitUntilNavigation[] waitUntilNavigations);
        Task<Page[]> GetPages();
        Task<Page> WaitGetPage(Page page, string selector, string targetId);
        Task<Page> ClickAndGetPage(ElementHandle element, string targetId);
        Task<Page> ClickAndGetPage(ElementHandle element, string targetId, WaitForOptions waitForOptions);
        Task<string> GetContentAsync(Page page);
        Task ClickAsync(Page page, string clickPlace, ClickOptions clickOptions = null);
        Task<string> GetFramePage(string iframeSrc);
        Task<string> GetFramePage(Page page, string frameName);
        Task<string> GetVideoAdsOnUrl(Page page, string xPath);
        Task<string> GetScrolledPage(string videoUrl, int maxScroll, params WaitUntilNavigation[] waitUntilNavigations);
        Task<string> GetPagePermission(Page page, string permissionName);

        Task<string> ClickButtonForContinue(Page page, string buttonXPath, string adUrl,
            params WaitUntilNavigation[] waitUntilNavigations);

        /// <summary>
        /// Returns IpDetails by the specified url
        /// </summary>
        /// <param name="ipFetcherUrl">The ip details provider url.</param>
        /// <returns>The instance of <see cref="IpDetails"></see></returns>
        Task<IpDetails> GetIpDetails(string ipFetcherUrl);

        /// <summary>
        /// Returns IpDetails by the specified url and, in case of error, returns the default country code and country name
        /// </summary>
        /// <param name="ipFetcherUrl">The ip details provider url.</param>
        /// <param name="defaultCountryCode">The country code tu return default details.</param>
        /// <returns>The instance of <see cref="IpDetails"></see></returns>
        Task<IpDetails> GetIpDetails(string ipFetcherUrl, string defaultCountryCode);
        Task CloseAsync();
        Task<Page> MouseClickAndGetPage(BoundingBox box, Page page);
    }
}
