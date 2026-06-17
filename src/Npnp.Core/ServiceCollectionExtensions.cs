using System;
using Microsoft.Extensions.DependencyInjection;
using Npnp.Core.Services;
using Npnp.Core.Writers;

namespace Npnp.Core;

/// <summary>
/// IServiceCollection 扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Npnp.Core 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddNpnpCore(this IServiceCollection services)
    {
        // 直接注册 HttpClient（而不是使用 AddHttpClient 工厂）
        // 避免 IHttpClientFactory 与 LcscApiService 构造函数中的配置冲突
        services.AddSingleton<HttpClient>(sp =>
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://pro.lceda.cn");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://pro.lceda.cn/");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            return httpClient;
        });

        // 注册 LcscApiService
        services.AddSingleton<ILcscApiService, LcscApiService>();

        // 注册 npnp CLI 调用服务（用于生成真正的 Altium 格式）
        services.AddSingleton<INpnpCliService, NpnpCliService>();

        // 注册真正的 Altium 格式 Writer 服务
        services.AddTransient<ISchLibWriter, RealAltiumSchLibWriter>();
        services.AddTransient<IPcbLibWriter, RealAltiumPcbLibWriter>();

        // 注册导出服务
        services.AddTransient<IExportService, ExportService>();

        return services;
    }
}