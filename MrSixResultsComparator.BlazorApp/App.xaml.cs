using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Services;
using MrSixResultsComparator.BlazorApp.Data;

namespace MrSixResultsComparator.BlazorApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Register Core services
        services.AddSingleton<AppConfiguration>();
        services.AddSingleton<MrSixContextService>();
        services.AddSingleton<ShardValidationService>();
        services.AddSingleton<SearchParameterService>(sp => 
            new SearchParameterService(sp.GetRequiredService<AppConfiguration>().SearchDataConnectionString));
        services.AddSingleton<SearchLogService>(sp => 
            new SearchLogService(sp.GetRequiredService<AppConfiguration>().SearchDataConnectionString));
        
        // Register all search services
        services.AddSingleton<StackSearchService>();
        services.AddSingleton<OnePushService>();
        services.AddSingleton<LitBatchService>();
        services.AddSingleton<LitSearchService>();
        services.AddSingleton<MoreLikeThisService>();
        services.AddSingleton<OneWayService>();
        services.AddSingleton<ExpertPicksService>();
        services.AddSingleton<JustForYouService>();
        services.AddSingleton<MatchPicksService>();
        services.AddSingleton<ReverseService>();
        services.AddSingleton<SearchWowService>();
        services.AddSingleton<TwoWayService>();
        
        services.AddTransient<ComparisonService>();
        
        // Register UI services
        services.AddSingleton<ComparisonStateService>();

        Resources.Add("services", services.BuildServiceProvider());
    }
}
