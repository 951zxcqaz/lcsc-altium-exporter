# Transform - 嘉立创元件库导出工具

基于 [npnp](https://github.com/linkyourbin/npnp) 引擎的 LCSC/嘉立创电子元器件搜索与 Altium Designer 库导出工具。

提供 **图形界面** 和 **命令行** 两种使用方式，**推荐大多数用户使用图形界面版本**。

## 📥 下载与运行（用户必读）

### 第一步：下载

前往 [Releases 页面](https://github.com/951zxcqaz/lcsc-altium-exporter/releases/latest) 下载：

| 文件 | 大小 | 适用人群 | 说明 |
|------|------|---------|------|
| **`transform-app-win-x64.zip`** | ~100 MB | ⭐ **推荐：大多数用户** | 图形界面版本，所见即所得，鼠标点点即可 |
| `npnp-cli-win-x64.zip` | ~62 MB | 高级用户 / 自动化 | 命令行版本，适合脚本批处理 |

> 💡 **两个 x64 zip 任选一个即可独立使用**，无需同时下载。**推荐下载图形界面版 `transform-app-win-x64.zip`**。

### 第二步：解压

将下载的 zip 完整解压到任意目录（**不要在压缩包内直接运行**），例如：

```
D:\Transform\
├── Transform.App.exe        ← 双击启动（图形界面）
├── npnp.exe                 ← 命令行入口（CLI 版解压后才有）
├── tools\
│   └── npnp.exe             ← 关键依赖：导出引擎
└── (其他 dll 文件)
```

> ⚠️ **重要**：`tools\npnp.exe` 必须保留，删除会导致导出失败。

### 第三步：运行

**图形界面版**：
- 双击 `Transform.App.exe` 即可打开主界面

**命令行版**（在解压目录打开 PowerShell）：
```powershell
.\npnp.exe --help
```

**系统要求**：
- Windows 10 / 11（64 位）
- 无需安装 .NET（自包含版本已包含运行时）
- 需要联网（用于访问 LCSC 搜索 API）

---

## 📖 图形界面使用教程（推荐）

界面分为四个区域：**搜索区 → 结果区 → 导出列表区 → 导出设置区**。

### 🎯 场景一：搜索并导出单个元件

**1. 搜索元件**

在搜索框输入关键词（两种方式任选）：

- **按 LCSC 编号**：输入 `C12702`（这是立创商城的标准编号，最准确）
- **按型号**：输入 `STM32F030C8T6` 或 `STM32F030`

> 💡 **搜索技巧**：
> - 搜索框下方可选择搜索字段（联合搜索、型号、品牌、封装等）
> - 选"联合搜索"会同时匹配多个字段，最常用
> - LCSC 编号搜索最精确，型号搜索最灵活

点击 **🔍 搜索** 按钮，结果会显示在下方的"搜索结果"列表。

**2. 选择并添加元件**

在搜索结果列表中：
- **查看详情**：每一行显示 LCSC 编号、型号、品牌、封装等
- **添加方式**：双击行，或选中后点击 **➕ 添加到导出列表**

添加后，元件会出现在"待导出列表"中。

**3. 配置导出选项（可选）**

在底部"导出设置"区域：

| 选项 | 作用 | 推荐 |
|------|------|------|
| **输出目录** | 生成的库文件保存位置 | 默认 `桌面\LcscExport` |
| **嵌入 STEP 模型** | 把 3D 模型嵌入 PCB 封装 | ✅ 推荐勾选 |
| **合并输出** | 把所有元件合并到 1 个库文件 | 按需勾选 |
| **库名称** | 合并后库文件的名字（仅合并时生效） | 例：`MyProject_Lib` |
| **追加模式** | 向已存在的合并库追加新元件（需配合合并输出） | 按需勾选 |

> 💡 **合并输出 vs 单独输出**：
> - 单独输出：每个元件一个 `.SchLib` 和 `.PcbLib`
> - 合并输出：所有元件放入同一个 `.SchLib` 和 `.PcbLib`，便于集中管理

**4. 执行导出**

点击 **📥 立即导出** 按钮：
- 进度条会显示当前状态
- 导出完成后，会自动在资源管理器打开输出目录
- 文件结构：
  ```
  LcscExport/
  ├── schlib/
  │   └── *.SchLib     ← 原理图符号库
  └── pcblib/
      └── *.PcbLib     ← PCB 封装库
  ```

**5. 在 Altium Designer 中使用**

1. 打开 AD 软件
2. 菜单栏：**Design → Make Schematic Library**（生成 .SchLib）/ **Make PCB Library**（生成 .PcbLib）
3. 或直接将生成的库文件拖到 AD 库面板

---

### 🎯 场景二：批量导出多个元件

**方法 1：多次搜索 + 手动添加**
1. 第一次搜索 `STM32F030`，添加搜索结果第一个
2. 第二次搜索 `AMS1117`，添加第一个
3. 第三次搜索 `C12702`，添加第一个
4. 点击"立即导出"

**方法 2：剪贴板自动读取（推荐）** ⭐

1. 勾选 **"剪贴板自动读取"** 复选框
2. 接下来复制任何 LCSC 编号或型号（Ctrl+C）
3. 程序会**每 400ms 自动检测剪贴板**
4. 检测到新内容后：
   - 自动搜索
   - 自动添加**第一个**结果到导出列表
5. 全部复制完成后，**取消勾选**自动读取，点击"立即导出"

> 💡 适用场景：从立创商城、Excel、PDF 中批量复制 LCSC 编号时，效率极高。

---

### ❓ 常见问题

**Q: 双击 exe 没反应？**
A: 打开 PowerShell，进入解压目录，执行 `.\Transform.App.exe`，查看错误信息（可能是杀毒软件拦截）。

**Q: 搜索无结果？**
A:
- 确认网络畅通（搜索需要访问 LCSC API）
- 关键词不要加前缀（如不要写 `型号:STM32`）
- 尝试更短的关键词（如 `STM32F030` 而非 `STM32F030C8T6TR`）

**Q: 导出失败？**
A:
- 检查 `tools\npnp.exe` 文件是否存在
- 检查输出目录是否有写入权限
- 查看导出日志中的具体错误

**Q: 杀毒软件报警？**
A: 自包含 .NET 应用体积较大，可能被误报。点击"信任"或添加白名单即可。

**Q: 库文件是空白的？**
A: 这是 npnp 引擎的旧问题，请升级到最新版（本项目已使用 npnp 官方工具）。

---

## 💻 命令行使用（高级用户）

> 适合自动化脚本、CI/CD、服务器批量处理等场景。

```bash
# 搜索元件
npnp search STM32 --limit 20

# 导出单个元件
npnp export C12702 --step

# 批量导出（components.txt 每行一个 LCSC 编号）
npnp batch --input components.txt --output ./output

# 批量导出 + 合并为单个库
npnp batch --input components.txt --output ./output --merge --library-name MyLib

# 向已存在库追加
npnp batch --input new_parts.txt --output ./output --merge --library-name MyLib --append
```

参数详解请运行 `npnp --help` 或 `npnp batch --help`。

---

## 🏗️ 项目结构（技术）

```
src/
├── Npnp.Core/          # 核心类库：LCSC API、导出服务、Altium 库写入器
├── Npnp.CLI/           # 命令行工具（基于 Spectre.Console）
└── Transform.App/      # WPF 图形界面应用（MVVM 架构）
```

## 🛠️ 技术栈（技术）

- **框架**：.NET 8.0 + WPF
- **架构**：MVVM（CommunityToolkit.Mvvm）
- **依赖注入**：Microsoft.Extensions.DependencyInjection
- **命令行**：Spectre.Console.Cli
- **日志**：Serilog
- **外部工具**：[npnp](https://github.com/linkyourbin/npnp)（Rust 实现的 LCSC 元件库导出器）

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/951zxcqaz/lcsc-altium-exporter.git
cd lcsc-altium-exporter

# 构建图形界面
dotnet build src/Transform.App/Transform.App.csproj --configuration Release

# 构建命令行工具
dotnet build src/Npnp.CLI/Npnp.CLI.csproj --configuration Release
```

## 📝 说明

- 本项目的核心功能依赖于 [npnp](https://github.com/linkyourbin/npnp) 提供的官方导出引擎
- 导出格式为 Altium Designer 原生格式（.SchLib / .PcbLib）
- 使用本工具需要能够访问 LCSC（立创商城）API 服务

## 📄 许可证

MIT License
