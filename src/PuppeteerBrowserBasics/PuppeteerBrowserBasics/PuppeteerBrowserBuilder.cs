using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ItWasMe.Common.StringUtils;
using ItWasMe.CommonLogging;
using ItWasMe.PuppeteerBrowserBasics.Models;
using ItWasMe.StaticReferences;
using ItWasMe.ThreadUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using Polly;
using Polly.Retry;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace ItWasMe.PuppeteerBrowserBasics
{
    public interface IChromiumBrowserBuilder
    {
        IPuppeteerContext CreateContext();
    }
    public class PuppeteerBrowserBuilder : IChromiumBrowserBuilder
    {
        private readonly IBrowserViewPortConfig _browserConfig;
        private readonly IDelayService _delayService;
        private readonly ILoggingContext _loggingContext;

        public PuppeteerBrowserBuilder(IBrowserViewPortConfig browserConfig, IDelayService delayService, ILoggingContext loggingContext)
        {
            _browserConfig = browserConfig;
            _delayService = delayService;
            _loggingContext = loggingContext;
        }

        public IPuppeteerContext CreateContext()
        {
            return new PuppeteerContext(_browserConfig, _delayService, _loggingContext);
        }

        private class PuppeteerContext : IPuppeteerContext
        {
            private const string DisableCrossOriginFeatures = "--disable-features=IsolateOrigins,site-per-process";
            private const string DisableWebSecurityArgument = "--disable-web-security";
            private const string DisableXssAuditorArgument = "--disable-xss-auditor";
            private const string DisableFeaturesArgument = "--disable-features";
            private readonly ILogger _logger;
            private readonly IBrowserViewPortConfig _browserConfig;
            private readonly IDelayService _delayService;
            private readonly ILoggingContext _loggingContext;

            private IEnumerable<OverridePermission> _overridePermissions;
            private readonly List<string> _browserProperties;
            private bool _headless;
            private volatile bool _isIncognito;
            public string ProxyUserName { get; private set; }
            public string ProxyPwd { get; private set; }
            public bool PageEvents { get; private set; }
            public bool BrowserEvents { get; private set; }

            public TimeSpan PageLoadTimeout { get; private set; }

            public bool PageCacheEnabled { get; private set; }
            public EventHandler<DialogEventArgs> DialogHandler { get; private set; }

            public PuppeteerContext(IBrowserViewPortConfig browserConfig, IDelayService delayService, ILoggingContext loggingContext)
            {
                _logger = loggingContext.CreateLogger<IPuppeteerContext>();
                _browserConfig = browserConfig;
                _delayService = delayService;
                _loggingContext = loggingContext;
                _browserProperties = new List<string>();
                _headless = true;
            }

            public IPuppeteerContext SetDefaultLaunchProperties()
            {
                _browserProperties.Add("--no-sandbox");
                _browserProperties.Add("--disable-infobars");
                _browserProperties.Add("--disable-setuid-sandbox");
                _browserProperties.Add("--ignore-certificate-errors");
                _browserProperties.Add("--disable-gpu");
                _browserProperties.Add("--lang=en-US,en");
                return this;
            }

            public IPuppeteerContext SetEnLangAndIgnoreCertErrors()
            {
                _browserProperties.Add("--ignore-certificate-errors");
                _browserProperties.Add("--lang=en-US,en");

                return this;
            }

            public IPuppeteerContext SetPageDialogHandler(EventHandler<DialogEventArgs> eventHandler)
            {
                DialogHandler = eventHandler;
                return this;
            }

            public IPuppeteerContext SetBrowserProperty(string property)
            {
                _browserProperties.Add(property);
                return this;
            }

            public IPuppeteerContext SetIncognito(bool incognito = true)
            {
                _isIncognito = incognito;
                _browserProperties.AddRange(new[] {
                    "--incognito"
                });
                return this;
            }

            public IPuppeteerContext SetOverridePermission()
            {
                _overridePermissions = new[]
                {
                    OverridePermission.ClipboardWrite, OverridePermission.ClipboardRead,
                    OverridePermission.AccessibilityEvents, OverridePermission.BackgroundSync,
                    OverridePermission.Geolocation, OverridePermission.Microphone, OverridePermission.Notifications,
                    OverridePermission.PaymentHandler, OverridePermission.Push
                };
                return this;
            }

            public IPuppeteerContext DisableWebSecurity()
            {
                if (!_browserProperties.Contains(DisableWebSecurityArgument))
                {
                    _browserProperties.Add(DisableWebSecurityArgument);
                }

                if (!_browserProperties.Contains(DisableCrossOriginFeatures))
                {
                    _browserProperties.Add(DisableCrossOriginFeatures);
                }

                return this;
            }

            public IPuppeteerContext DisableXssAuditor()
            {
                if (!_browserProperties.Contains(DisableXssAuditorArgument))
                {
                    _browserProperties.Add(DisableXssAuditorArgument);
                }

                return this;
            }

            public IPuppeteerContext DisableFeatures(string arg)
            { 
                var webProp = $"{DisableFeaturesArgument}={arg}";
                if (!_browserProperties.Contains(webProp))
                {
                    _browserProperties.Add(webProp);
                }

                return this;
            }

            public IPuppeteerContext SetHeadless(bool headless)
            {
                _headless = headless;
                return this;
            }

            public IPuppeteerContext SetHeadless(TimeSpan timeout)
            {
                PageLoadTimeout = timeout;
                return this;
            }

            public IPuppeteerContext SetPageCacheEnabled(bool enabled)
            {
                PageCacheEnabled = enabled;
                return this;
            }

            public IPuppeteerContext EnableBrowserEvents(bool browserEvents)
            {
                BrowserEvents = browserEvents;
                return this;
            }

            public IPuppeteerContext EnablePageEvents(bool pageEvents)
            {
                PageEvents = pageEvents;
                return this;
            }

            public IPuppeteerContext SetProxy(string ip, int port, string userName, string pwd)
            {
                _browserProperties.Add($"--proxy-server={ip}:{port}");
                ProxyUserName = userName;
                ProxyPwd = pwd;
                return this;
            } 

            public async Task<IPuppeteerBrowser> CreateBrowser(Platform platform)
            {
                var execPath = await PuppeteerHelper.GetChromiumPath(platform);
                var launchOptions = new LaunchOptions
                {
                    Headless = _headless,
                    ExecutablePath = execPath,
                    IgnoreHTTPSErrors = true,
                    DefaultViewport = new ViewPortOptions { Height = _browserConfig.WindowHeight, Width = _browserConfig.WindowWidth },
                    Args = _browserProperties.ToArray()
                };

                var browser = await Puppeteer.LaunchAsync(launchOptions);
                var context = _isIncognito
                    ? await browser.CreateIncognitoBrowserContextAsync()
                    : browser.DefaultContext;

                return new ChromiumBrowser(_browserConfig, _delayService, _loggingContext, this, browser, context, _overridePermissions);
            }
        }

        private class ChromiumBrowser : IPuppeteerBrowser
        {
            private static readonly Regex RegexStripHtmlTags = new Regex("<.*?>");
            private readonly ILogger _logger;
            private readonly IBrowserViewPortConfig _browserConfig;
            private readonly IDelayService _delayService;
            private readonly Browser _browser;
            private readonly BrowserContext _browserContext;
            private readonly IPuppeteerContext _puppeteerContext;
            private readonly IEnumerable<OverridePermission> _overridePermissions;
            private readonly AsyncRetryPolicy _policyNoData;
            private readonly decimal[] _diagonalClickDevisors = new decimal[] {5, 4, 3, 2, 1.6M, 1.4M, 1.2M, 1.1M};

            public ChromiumBrowser(IBrowserViewPortConfig browserConfig, IDelayService delayService, ILoggingContext loggingContext, IPuppeteerContext puppeteerContext, 
                Browser browser, BrowserContext browserContext, IEnumerable<OverridePermission> overridePermissions)
            {
                _logger = loggingContext.CreateLogger<IPuppeteerBrowser>();
                _browserConfig = browserConfig;
                _delayService = delayService;
                _puppeteerContext = puppeteerContext;
                _browser = browser;
                _browserContext = browserContext;
                _overridePermissions = overridePermissions;
                if (puppeteerContext.BrowserEvents)
                {
                    _browser.Closed += _browser_Closed;
                    _browser.Disconnected += _browser_Disconnected;
                    _browser.TargetChanged += _browser_TargetChanged;
                    _browser.TargetCreated += _browser_TargetCreated;
                    _browser.TargetDestroyed += _browser_TargetDestroyed;
                }
                _policyNoData = Policy
                    .Handle<TimeoutException>().RetryAsync(_diagonalClickDevisors.Length,
                        (exception, retryN, context) =>
                        {
                            _logger.Warn($"Diagonal Clicks Try#{retryN}");
                        });
            }

            private void _browser_TargetDestroyed(object sender, TargetChangedArgs e)
            {
                _logger.Warn($"Browser: Target has been destroyed");
            }

            private void _browser_TargetCreated(object sender, TargetChangedArgs e)
            {
                _logger.Warn($"Browser: Target has been created");
            }

            private void _browser_TargetChanged(object sender, TargetChangedArgs e)
            {
                _logger.Warn($"Browser: Target has been changed");
            }

            private void _browser_Disconnected(object sender, EventArgs e)
            {
                _logger.Warn("Browser has been disconnected");
            }

            private void _browser_Closed(object sender, EventArgs e)
            {
                _logger.Warn("Browser has been closed");
            }

            /// <summary>
            /// Setup the chromium for browser automation
            /// </summary>
            /// <returns></returns>
            async Task IPuppeteerBrowser.ClickAsync(Page page, string clickPlace, ClickOptions clickOptions)
            {
                await page.ClickAsync(clickPlace, clickOptions);
            } 

            async Task<string> IPuppeteerBrowser.GetContentAsync(Page page)
            {
                return await page.GetContentAsync();
            }

            async Task<Page> IPuppeteerBrowser.WaitGetPage(Page page, string selector, string targetId)
            { 
                var newTarget = await _browserContext.WaitForTargetAsync(a => a.Opener?.TargetId == targetId, new WaitForOptions { Timeout = 10 * 1000 });
                var newPage = await newTarget.PageAsync();
                await SetPageOptions(newPage);
                await newPage.WaitForSelectorAsync(selector);
                return newPage;
            }

            async Task IPuppeteerBrowser.CloseAsync()
            {
                await _browser.CloseAsync();
            }

            async Task<Page> IPuppeteerBrowser.ClickAndGetPage(ElementHandle element, string targetId)
            {
                await element.ClickAsync();
                var newTarget = await _browserContext.WaitForTargetAsync(a => a.Opener?.TargetId == targetId);
                var newPage = await newTarget.PageAsync();
                await SetPageOptions(newPage);
                return newPage;
            }

            async Task<Page> IPuppeteerBrowser.MouseClickAndGetPage(BoundingBox box, Page page)
            {
                int retryN = 0;
                return await _policyNoData.ExecuteAsync(async () =>
                {
                    var pages = await _browserContext.PagesAsync();
                    var pageCount = pages.Length;
                    var listId = new List<string>();
                    foreach (var page1 in pages)
                    {
                        var id = page1.Target?.TargetId;
                        listId.Add(id);
                    }
                    await page.Keyboard.DownAsync("Control");
                    await page.Mouse.ClickAsync(box.X + (box.Width / _diagonalClickDevisors[retryN]), box.Y + (box.Height / _diagonalClickDevisors[retryN]));
                    await page.Keyboard.UpAsync("Control");
                    var pages1 = await _browserContext.PagesAsync();
                    if (pages1.Length <= pageCount)
                    {
                        retryN++;
                        throw new TimeoutException();
                    }
                    var newPage = pages1.FirstOrDefault(a => !listId.Contains(a.Target?.TargetId));
                    if (newPage == null)
                    {
                        throw new InvalidOperationException("Page not found after click");
                    }

                    if (_puppeteerContext.DialogHandler != null)
                    {
                        newPage.Dialog += _puppeteerContext.DialogHandler;
                    }

                    await SetPageOptions(newPage);
                    return newPage;
                });
            }

            async Task<Page> IPuppeteerBrowser.ClickAndGetPage(ElementHandle element, string targetId, WaitForOptions waitForOptions)
            {
                await element.ClickAsync();
                var newTarget = await _browserContext.WaitForTargetAsync(a => a.Opener?.TargetId == targetId, waitForOptions);
                var newPage = await newTarget.PageAsync();
                await SetPageOptions(newPage);
                return newPage;
            }

            async Task<string> IPuppeteerBrowser.ClickButtonForContinue(Page page, string buttonXPath, string adUrl, params WaitUntilNavigation[] waitUntilNavigations)
            {
                var continueButtons = page.XPathAsync(buttonXPath);
                _logger.Info($"Needed click on button for continue. Url: {adUrl}");

                foreach (var continueButton in await continueButtons)
                {
                    _logger.Debug($"Will click on {buttonXPath}");

                    await continueButton.ClickAsync();
                    var navigationTask = page.WaitForNavigationAsync(new NavigationOptions
                    {
                        WaitUntil = waitUntilNavigations
                    });
                    if (await Task.WhenAny(navigationTask, _delayService.DelayAsync(TimeSpan.FromSeconds(30))) == navigationTask)
                    {
                        _logger.Info($"Returned new url:'{page.Url}'");
                        if (!page.Url.Contains("redirect/verify"))
                        {
                            return page.Url;
                        }
                    }
                    return string.Empty;

                }
                return string.Empty;

            }

            async Task<Page[]> IPuppeteerBrowser.GetPages()
            {
                return await _browserContext.PagesAsync();
            }

            async Task<string> IPuppeteerBrowser.GetFramePage(string iframeSrc)
            {

                using (var multiVideopage = await _browserContext.NewPageAsync())
                {
                    await SetPageOptions(multiVideopage);
                    await multiVideopage.GoToAsync(iframeSrc, Convert.ToInt32(_puppeteerContext.PageLoadTimeout.TotalMilliseconds), new WaitUntilNavigation[] { WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0, WaitUntilNavigation.Networkidle2 });

                    var data = await multiVideopage.GetContentAsync();
                    return data;
                }
            }

            async Task<string> IPuppeteerBrowser.GetFramePage(Page page, string frameName)
            {
                var googlAdsFrame1 = page.Frames.ToList().FirstOrDefault(x => x.Name == frameName);
                if (googlAdsFrame1 == null)
                {
                    return null;
                }
                return await googlAdsFrame1.GetContentAsync();
            }
            
            async Task<string> IPuppeteerBrowser.GetVideoAdsOnUrl(Page page, string xPath)
            {
                var elementToclick = await page.WaitForXPathAsync(xPath);
                if (elementToclick != null)      
                {
                    await elementToclick.ClickAsync();
                    return await page.EvaluateFunctionAsync<string>("() => navigator.clipboard.readText()");
                }
                _logger.Debug("GetVideoAdsOnUrl: elementToclick is null");
                return null;
            }


            /// <summary>
            /// Returns IpDetails by the specified url
            /// </summary>
            /// <param name="ipFetcherUrl">The ip details provider url.</param>
            /// <returns>The instance of <see cref="IpDetails"></see></returns>
            async Task<IpDetails> IPuppeteerBrowser.GetIpDetails(string ipFetcherUrl)
            {
                IpDetails ipDetails = await GetIpDetailsInternal(ipFetcherUrl);

                return ipDetails ?? new IpDetails
                {
                    City = "N/A",
                    ContinentName = "N/A",
                    CountryCode = "N/A",
                    CountryName = "N/A",
                    IpAddress = "127.0.0.1",
                    State = "N/A",
                    ContinentCode = "N/A"
                };
            }

            /// <summary>
            /// Returns IpDetails by the specified url and, in case of error, returns the default country code and country name
            /// </summary>
            /// <param name="ipFetcherUrl">The ip details provider url.</param>
            /// <param name="defaultCountryCode">The country code tu return default details.</param>
            /// <returns>The instance of <see cref="IpDetails"></see></returns>
            async Task<IpDetails> IPuppeteerBrowser.GetIpDetails(string ipFetcherUrl, string defaultCountryCode)
            {
                IpDetails ipDetails = await GetIpDetailsInternal(ipFetcherUrl);

                return ipDetails ?? new IpDetails
                {
                    City = "N/A",
                    ContinentName = "N/A",
                    CountryCode = defaultCountryCode,
                    CountryName = References.Countries[defaultCountryCode],
                    IpAddress = "127.0.0.1",
                    State = "N/A",
                    ContinentCode = "N/A"
                };
            }

            async Task<string> IPuppeteerBrowser.GetPageContent(string stringUrl, params WaitUntilNavigation[] waitUntilNavigations)
            {
                return await GetPageContentInternalAsync(stringUrl, waitUntilNavigations);
            }

            private async Task SetPageOptions(Page page)
            {
                var viewPortOptions = new ViewPortOptions
                { Width = _browserConfig.WindowWidth, Height = _browserConfig.WindowHeight };
                await page.SetViewportAsync(viewPortOptions);
                if (!string.IsNullOrWhiteSpace(_puppeteerContext.ProxyUserName))
                {
                    await page.AuthenticateAsync(new Credentials
                    {
                        Username = _puppeteerContext.ProxyUserName,
                        Password = _puppeteerContext.ProxyPwd
                    });
                }
            }

            async Task<Page> IPuppeteerBrowser.GetPage(string url, TimeSpan? timeoutPageLoad, params WaitUntilNavigation[] waitUntilNavigations)
            {
                var page = await GoToPageAsync(url, timeoutPageLoad, waitUntilNavigations);
                if (_overridePermissions?.Any() ?? false)
                {
                    await page.BrowserContext.OverridePermissionsAsync(page.Url,
                        new []
                        {
                            OverridePermission.ClipboardWrite, OverridePermission.ClipboardRead,
                            OverridePermission.AccessibilityEvents, OverridePermission.BackgroundSync,
                            OverridePermission.Geolocation, OverridePermission.Microphone, OverridePermission.Notifications,
                            OverridePermission.PaymentHandler/*, OverridePermission.Push*/
                        });
                }
                return page;
            }

            async Task<string> IPuppeteerBrowser.GetScrolledPage(string url, int maxScroll, params WaitUntilNavigation[] waitUntilNavigations)
            {
                var pageResult = string.Empty;
                try
                {
                    using (var page = await GoToPageAsync(url, null, waitUntilNavigations))
                    {
                        await ScrollPageSync(page, TimeSpan.FromMilliseconds(100));

                        pageResult = await page.GetContentAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }

                return pageResult;
            }

            async Task IPuppeteerBrowser.ScrollPageSync(Page page, int maxScroll = 40000, int step = 200)
            {
                await ScrollPageSync(page, TimeSpan.FromMilliseconds(100), maxScroll, step);
            }

            /// <summary>
            /// Return state of page permission. Exmp: permissionName=clipboard-read
            /// </summary>
            /// <returns></returns>
            async Task<string> IPuppeteerBrowser.GetPagePermission(Page page, string permissionName)
            {
                return await page.EvaluateFunctionAsync<string>($"name => navigator.permissions.query({permissionName}).then(result => result.state)");
            }

            public async Task ScrollPageSync(Page page, TimeSpan stepDelay, int maxScroll = 40000, int step = 200)
            {
                var height = await page.EvaluateExpressionAsync<int>(@"document.body.scrollHeight");
                var x = 0;
                while (x < height && x <= maxScroll)
                {
                    height = await page.EvaluateExpressionAsync<int>(@"document.body.scrollHeight");
                    x += 200;
                    await _delayService.DelayAsync(stepDelay);
                    await page.EvaluateExpressionAsync($"window.scrollTo(0, {x});");
                }
            }

            private async Task<Page> GoToPageAsync(string stringUrl, TimeSpan? timeSpan, params WaitUntilNavigation[] waitUntilNavigations)
            {
                var page = await _browserContext.NewPageAsync();
                await page.SetCacheEnabledAsync(_puppeteerContext.PageCacheEnabled);
                await page.SetJavaScriptEnabledAsync(true);

                if (_puppeteerContext.PageEvents)
                {
                    page.Error += Page_Error;
                    page.PageError += Page_PageError;
                    page.Console += Page_Info;
                    page.Request += Page_Request;
                }

                if (_puppeteerContext.DialogHandler != null)
                {
                    page.Dialog += _puppeteerContext.DialogHandler;
                }

                await SetPageOptions(page);
                var resp = await page.GoToAsync(stringUrl,
                    Convert.ToInt32((timeSpan ?? TimeSpan.FromSeconds(30)).Milliseconds), waitUntilNavigations);
                LogGoToResponse(stringUrl, resp);
                return page;

            }

            private void Page_PageError(object sender, PageErrorEventArgs e)
            {
                _logger.Error(e.Message);
            }

            private void Page_Request(object sender, RequestEventArgs e)
            {
                _logger.Debug($"{e.Request?.Method}:{e.Request?.ResourceType.ToString()}:{e.Request?.Url}");
            }

            private void LogGoToResponse(string stringUrl, Response resp)
            {
                if (resp != null)
                {
                    var respLogEvent = new LogEventInfo(LogLevel.Info, string.Empty, $"goto response to {stringUrl}");
                    respLogEvent.Properties["FromCache"] = resp.FromCache;
                    respLogEvent.Properties["Url"] = resp.Url;
                    respLogEvent.Properties["RemoteAddress.IP"] = resp.RemoteAddress?.IP;
                    respLogEvent.Properties["RemoteAddress.Port"] = resp.RemoteAddress?.Port;
                    respLogEvent.Properties["Request.RequestId"] = resp.Request?.RequestId;
                    respLogEvent.Properties["Status"] = resp.Status;
                    respLogEvent.Properties["Ok"] = resp.Ok;
                    respLogEvent.Properties["FromServiceWorker"] = resp.FromServiceWorker;

                    if (resp.Headers != null)
                    {
                        foreach (var header in resp.Headers)
                        {
                            respLogEvent.Properties[$"Header:{header.Key}"] = header.Value;
                        }
                    }

                    _logger.Log(respLogEvent);
                }
            }

            private void Page_Info(object sender, ConsoleEventArgs e)
            {
                _logger.Info($"{e.Message.Type}: {e.Message.Text}");
            }

            private void Page_Error(object sender, PuppeteerSharp.ErrorEventArgs e)
            {
                _logger.Error(e.Error);
            }

            public void Dispose()
            {
                _logger.Info($"Disposing browser {_browser.Process.Id}");
                _browser?.Dispose();
            }
            private string StripHtmlTags(string input) => RegexStripHtmlTags.Replace(input, string.Empty); 
            private async Task<string> GetPageContentInternalAsync(string stringUrl, params WaitUntilNavigation[] waitUntilNavigations)
            {
                using (var page = await _browserContext.NewPageAsync())
                {
                    await SetPageOptions(page);
                    var resp = await page.GoToAsync(stringUrl, null, waitUntilNavigations);
                    LogGoToResponse(stringUrl, resp);
                    return await page.GetContentAsync();
                }
            }

            private async Task<IpDetails> GetIpDetailsInternal(string ipFetcherUrl)
            {
                IpDetails ipDetails = null;
                try
                {
                    var ipPageSource = await GetPageContentInternalAsync(ipFetcherUrl, WaitUntilNavigation.Networkidle0);
                    _logger.Info(ipPageSource);
                    var ipPage = StripHtmlTags(ipPageSource);

                    if (!ipPage.Contains("OVER_QUERY_LIMIT"))
                    {
                        ipDetails = JsonConvert.DeserializeObject<IpDetails>(ipPage);
                        return new IpDetails()
                        {
                            CountryCode = ipDetails.CountryCode,
                            City = ipDetails.City.NormalizeToForm(),
                            ContinentCode = ipDetails.ContinentCode,
                            ContinentName = ipDetails.ContinentName.NormalizeToForm(),
                            CountryName = ipDetails.CountryName.NormalizeToForm(),
                            IpAddress = ipDetails.IpAddress,
                            State = ipDetails.State.NormalizeToForm()
                        };
                    }

                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }

                return ipDetails;
            }
        }
    }
}
