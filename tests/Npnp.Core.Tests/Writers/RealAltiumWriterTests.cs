using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Npnp.Core.Models;
using Npnp.Core.Writers;
using Xunit;
using Xunit.Abstractions;

namespace Npnp.Core.Tests.Writers
{
    public class RealAltiumWriterTests
    {
        private readonly ITestOutputHelper _output;

        public RealAltiumWriterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestCompoundFileCreation()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.SchLib");
            _output.WriteLine($"测试文件: {tempPath}");

            try
            {
                // 测试基本的 CFB 文件创建
                using var cf = new OpenMcdf.CompoundFile();

                // 创建一个简单的流
                var rootStream = cf.RootStorage.AddStream("TestStream");
                var data = System.Text.Encoding.UTF8.GetBytes("Hello, Altium!");
                rootStream.SetData(data);

                // 保存文件
                cf.SaveAs(tempPath);

                // 验证文件存在
                Assert.True(File.Exists(tempPath), "CFB 文件应已创建");
                _output.WriteLine($"文件大小: {new FileInfo(tempPath).Length} bytes");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void TestSchLibWriter_WithBasicComponent()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"schlib_{Guid.NewGuid()}.SchLib");
            _output.WriteLine($"测试 SchLib 输出: {tempPath}");

            try
            {
                var writer = new RealAltiumSchLibWriter();
                var components = new List<ComponentDetail>
                {
                    new ComponentDetail(
                        lcscId: "C98192",
                        name: "CL21A475KBQNNNE",
                        description: "测试元件",
                        manufacturer: "SAMSUNG(三星)")
                    {
                        Package = "0805"
                    }
                };

                var options = new ExportOptions
                {
                    LibraryName = "TestLib",
                    ForceOverwrite = true
                };

                writer.Write(tempPath, components, options);

                Assert.True(File.Exists(tempPath), "SchLib 文件应已创建");
                _output.WriteLine($"SchLib 文件大小: {new FileInfo(tempPath).Length} bytes");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"异常: {ex}");
                throw;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void TestPcbLibWriter_WithBasicComponent()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"pcblib_{Guid.NewGuid()}.PcbLib");
            _output.WriteLine($"测试 PcbLib 输出: {tempPath}");

            try
            {
                var writer = new RealAltiumPcbLibWriter();
                var components = new List<ComponentDetail>
                {
                    new ComponentDetail(
                        lcscId: "C98192",
                        name: "CL21A475KBQNNNE",
                        description: "测试元件",
                        manufacturer: "SAMSUNG(三星)")
                    {
                        Package = "0805"
                    }
                };

                var options = new ExportOptions
                {
                    LibraryName = "TestLib",
                    ForceOverwrite = true
                };

                writer.Write(tempPath, components, options);

                Assert.True(File.Exists(tempPath), "PcbLib 文件应已创建");
                _output.WriteLine($"PcbLib 文件大小: {new FileInfo(tempPath).Length} bytes");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"异常: {ex}");
                throw;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public async Task TestApiResponse_RealData()
        {
            var debugDir = Path.Combine(Path.GetTempPath(), "npnp_api_debug_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(debugDir);
            _output.WriteLine($"调试目录: {debugDir}");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(30);

            // 步骤 1: 搜索 C2040
            var searchUrl = "https://pro.lceda.cn/api/szlcsc/eda/product/list?wd=C2040&limit=1";
            _output.WriteLine($"\n=== 步骤 1: 搜索 ===");
            _output.WriteLine($"URL: {searchUrl}");
            var searchResp = await client.GetAsync(searchUrl);
            var searchContent = await searchResp.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync(Path.Combine(debugDir, "01_search.json"), searchContent);
            _output.WriteLine($"Search Status: {searchResp.StatusCode}");
            _output.WriteLine($"Search Length: {searchContent.Length}");

            // 解析 search 响应，获取 product uuid
            using var searchDoc = JsonDocument.Parse(searchContent);
            string? productUuid = null;
            string? symbolUuid = null;
            string? footprintUuid = null;
            if (searchDoc.RootElement.TryGetProperty("result", out var searchRes) && searchRes.ValueKind == JsonValueKind.Array)
            {
                var first = searchRes.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    productUuid = first.TryGetProperty("uuid", out var uuidEl) ? uuidEl.GetString() : null;

                    // 从 symbol/footprint 对象
                    if (first.TryGetProperty("symbol", out var symEl) && symEl.ValueKind == JsonValueKind.Object)
                    {
                        symbolUuid = symEl.TryGetProperty("uuid", out var suuidEl) ? suuidEl.GetString() : null;
                    }
                    if (first.TryGetProperty("footprint", out var fpEl) && fpEl.ValueKind == JsonValueKind.Object)
                    {
                        footprintUuid = fpEl.TryGetProperty("uuid", out var fpuuidEl) ? fpuuidEl.GetString() : null;
                    }

                    // 从 attributes 获取
                    if (first.TryGetProperty("attributes", out var attrsEl) && attrsEl.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrEmpty(symbolUuid) && attrsEl.TryGetProperty("Symbol", out var sa) && sa.ValueKind == JsonValueKind.String)
                        {
                            symbolUuid = sa.GetString();
                        }
                        if (string.IsNullOrEmpty(footprintUuid) && attrsEl.TryGetProperty("Footprint", out var fa) && fa.ValueKind == JsonValueKind.String)
                        {
                            footprintUuid = fa.GetString();
                        }
                    }
                }
            }

            _output.WriteLine($"Product UUID: {productUuid}");
            _output.WriteLine($"Symbol UUID: {symbolUuid}");
            _output.WriteLine($"Footprint UUID: {footprintUuid}");

            // 步骤 2: 用 product uuid 获取详情
            if (!string.IsNullOrEmpty(productUuid))
            {
                _output.WriteLine($"\n=== 步骤 2: 获取 product 详情 ===");
                var productUrl = $"https://pro.lceda.cn/api/components/{productUuid}?uuid={productUuid}";
                var productResp = await client.GetAsync(productUrl);
                var productContent = await productResp.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(Path.Combine(debugDir, "02_product.json"), productContent);
                _output.WriteLine($"Product Status: {productResp.StatusCode}");
                _output.WriteLine($"Product Length: {productContent.Length}");

                // 解析 product 响应，看是否有 symbol_uuid / footprint_uuid
                using var prodDoc = JsonDocument.Parse(productContent);
                if (prodDoc.RootElement.TryGetProperty("result", out var prodRes) && prodRes.ValueKind == JsonValueKind.Object)
                {
                    // 打印所有顶层字段
                    _output.WriteLine($"Product result keys: {string.Join(", ", prodRes.EnumerateObject().Select(p => p.Name))}");

                    // 检查 dataStr
                    if (prodRes.TryGetProperty("dataStr", out var dsEl) && dsEl.ValueKind == JsonValueKind.String)
                    {
                        var ds = dsEl.GetString();
                        _output.WriteLine($"Product dataStr length: {ds?.Length ?? 0}");
                    }

                    // 从 attributes 重新获取
                    if (prodRes.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                    {
                        if (attrs.TryGetProperty("Symbol", out var sa) && sa.ValueKind == JsonValueKind.String)
                        {
                            symbolUuid = sa.GetString();
                            _output.WriteLine($"Updated Symbol UUID from product: {symbolUuid}");
                        }
                        if (attrs.TryGetProperty("Footprint", out var fa) && fa.ValueKind == JsonValueKind.String)
                        {
                            footprintUuid = fa.GetString();
                            _output.WriteLine($"Updated Footprint UUID from product: {footprintUuid}");
                        }
                    }

                    // 从 symbol/footprint 重新获取
                    if (prodRes.TryGetProperty("symbol", out var symObj) && symObj.ValueKind == JsonValueKind.Object)
                    {
                        if (symObj.TryGetProperty("uuid", out var suuidEl) && suuidEl.ValueKind == JsonValueKind.String)
                        {
                            symbolUuid = suuidEl.GetString();
                            _output.WriteLine($"Updated Symbol UUID from product.symbol: {symbolUuid}");
                        }
                    }
                    if (prodRes.TryGetProperty("footprint", out var fpObj) && fpObj.ValueKind == JsonValueKind.Object)
                    {
                        if (fpObj.TryGetProperty("uuid", out var fpuuidEl) && fpuuidEl.ValueKind == JsonValueKind.String)
                        {
                            footprintUuid = fpuuidEl.GetString();
                            _output.WriteLine($"Updated Footprint UUID from product.footprint: {footprintUuid}");
                        }
                    }
                }
            }

            // 步骤 3: 用 symbol uuid 获取 symbol 详情
            if (!string.IsNullOrEmpty(symbolUuid))
            {
                _output.WriteLine($"\n=== 步骤 3: 获取 symbol 详情 ===");
                var symUrl = $"https://pro.lceda.cn/api/components/{symbolUuid}?uuid={symbolUuid}";
                var symResp = await client.GetAsync(symUrl);
                var symContent = await symResp.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(Path.Combine(debugDir, "03_symbol.json"), symContent);
                _output.WriteLine($"Symbol Status: {symResp.StatusCode}");
                _output.WriteLine($"Symbol Length: {symContent.Length}");

                using var symDoc = JsonDocument.Parse(symContent);
                if (symDoc.RootElement.TryGetProperty("result", out var symRes) && symRes.ValueKind == JsonValueKind.Object)
                {
                    _output.WriteLine($"Symbol result keys: {string.Join(", ", symRes.EnumerateObject().Select(p => p.Name))}");
                    if (symRes.TryGetProperty("dataStr", out var dsEl) && dsEl.ValueKind == JsonValueKind.String)
                    {
                        var ds = dsEl.GetString();
                        _output.WriteLine($"Symbol dataStr length: {ds?.Length ?? 0}");
                        _output.WriteLine($"Symbol dataStr first 200 chars: {ds?.Substring(0, Math.Min(200, ds.Length))}");
                    }
                }
            }

            // 步骤 4: 用 footprint uuid 获取 footprint 详情
            if (!string.IsNullOrEmpty(footprintUuid))
            {
                _output.WriteLine($"\n=== 步骤 4: 获取 footprint 详情 ===");
                var fpUrl = $"https://pro.lceda.cn/api/components/{footprintUuid}?uuid={footprintUuid}";
                var fpResp = await client.GetAsync(fpUrl);
                var fpContent = await fpResp.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(Path.Combine(debugDir, "04_footprint.json"), fpContent);
                _output.WriteLine($"Footprint Status: {fpResp.StatusCode}");
                _output.WriteLine($"Footprint Length: {fpContent.Length}");

                using var fpDoc = JsonDocument.Parse(fpContent);
                if (fpDoc.RootElement.TryGetProperty("result", out var fpRes) && fpRes.ValueKind == JsonValueKind.Object)
                {
                    _output.WriteLine($"Footprint result keys: {string.Join(", ", fpRes.EnumerateObject().Select(p => p.Name))}");
                    if (fpRes.TryGetProperty("dataStr", out var dsEl) && dsEl.ValueKind == JsonValueKind.String)
                    {
                        var ds = dsEl.GetString();
                        _output.WriteLine($"Footprint dataStr length: {ds?.Length ?? 0}");
                        _output.WriteLine($"Footprint dataStr first 200 chars: {ds?.Substring(0, Math.Min(200, ds.Length))}");
                    }
                }
            }

            _output.WriteLine($"\n所有调试数据已保存到: {debugDir}");
        }
    }
}
