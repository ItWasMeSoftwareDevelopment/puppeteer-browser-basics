using PuppeteerSharp;
using System;
using System.Threading.Tasks;
using NLog;

namespace ItWasMe.PuppeteerBrowserBasics
{
    public interface IPuppeteerContext
    {
        string ProxyUserName { get; }
        string ProxyPwd { get; }
        bool PageEvents { get; }
        bool BrowserEvents { get; }
        TimeSpan PageLoadTimeout { get; }
        bool PageCacheEnabled { get; }
        EventHandler<DialogEventArgs> DialogHandler { get; }
        IPuppeteerContext SetOverridePermission();
        IPuppeteerContext SetDefaultLaunchProperties();
        IPuppeteerContext SetEnLangAndIgnoreCertErrors();
        IPuppeteerContext SetPageDialogHandler(EventHandler<DialogEventArgs> eventHandler);
        IPuppeteerContext SetBrowserProperty(string property);
        IPuppeteerContext SetIncognito(bool incognito = true);
        IPuppeteerContext SetProxy(string ip, int port, string userName, string pwd);
        IPuppeteerContext SetHeadless(bool headless);
        IPuppeteerContext SetHeadless(TimeSpan timeout);
        IPuppeteerContext SetPageCacheEnabled(bool enabled);
        Task<IPuppeteerBrowser> CreateBrowser(Platform platform);
        IPuppeteerContext DisableWebSecurity();
        IPuppeteerContext DisableFeatures(string arg);        
        IPuppeteerContext DisableXssAuditor();
        IPuppeteerContext EnableBrowserEvents(bool browserEvents);
        IPuppeteerContext EnablePageEvents(bool pageEvents);
    }
}
