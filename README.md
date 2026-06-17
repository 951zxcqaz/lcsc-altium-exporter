# Transform - 嘉立创元件库导出工具

基于 [npnp](https://github.com/linkyourbin/npnp) 引擎的 LCSC/嘉立创电子元器件搜索与 Altium 库导出工具，提供图形界面（GUI）和命令行（CLI）两种使用方式。

## ✨ 功能特性

### 🖥️ 图形界面（Transform.App）

- **智能搜索**：支持 LCSC 编号、型号、品牌、封装等 15+ 字段联合搜索
- **剪贴板自动读取**：选中后自动读取剪贴板内容，搜索并添加第一个结果到导出列表
- **批量导出**：一键生成 Altium Designer 格式的原理图库（.SchLib）和 PCB 封装库（.PcbLib）
- **合并输出**：将多个元件合并为单个库文件，支持自定义库名称
- **追加模式**：向已有的合并库追加新元件
- **STEP 3D 模型**：可选嵌入 3D STEP 模型到 PCB 封装
- **实时进度**：导出过程中显示实时进度条与状态信息

### 💻 命令行（npnp CLI）

- `search` - 按关键词搜索 LCSC 元器件
- `download` - 下载 3D 模型（STEP / OBJ）
- `export` - 导出单个元件的 Altium 库
- `batch` - 从文件批量导出元件库

### 🔧 核心库（Npnp.Core）

可作为类库集成到你的 .NET 项目中：

- `ILcscApiService` - LCSC 搜索与元件详情 API
- `INpnpCliService` - npnp.exe 调用封装
- `IExportService` - 元件数据导出服务

## 🚀 快速开始

### 环境要求

- Windows 10/11（WPF 图形界面仅支持 Windows）
- .NET 8.0 SDK 或更高版本（从源码构建时需要）
- `tools/npnp.exe`（官方 Rust 版，从 [npnp Releases](https://github.com/linkyourbin/npnp/releases) 下载并放置到 `tools/` 目录）

### 方式一：下载 Release 版本

直接从 [GitHub Releases](https://github.com/your-org/transform/releases) 下载最新的自包含版本，无需安装 .NET。

### 方式二：从源码构建

```bash
# 克隆仓库
git clone https://github.com/your-org/transform.git
cd transform

# 构建图形界面
dotnet build src/Transform.App/Transform.App.csproj --configuration Release

# 构建命令行工具
dotnet build src/Npnp.CLI/Npnp.CLI.csproj --configuration Release
```

### 放置 npnp.exe

将从 [npnp Releases](https://github.com/linkyourbin/npnp/releases) 下载的 `npnp.exe` 放置到：

```
Transform 输出目录/
└── tools/
    └── npnp.exe      ← 放置在此
```

## 📖 使用指南

### 图形界面使用

1. **搜索元件**：在顶部搜索框输入关键词（如 `STM32F030C8T6`、`C12702`），选择搜索字段后点击"🔍 搜索"
2. **添加到导出列表**：双击搜索结果或选中后点击"➕ 添加到导出"
3. **配置导出选项**：
   - 勾选"嵌入 STEP 模型"以包含 3D 模型
   - 勾选"合并输出"将所有元件合并为单个库文件，并可自定义库名称
   - 勾选"追加模式"（需配合合并输出）以向现有库追加元件
4. **点击"📥 立即导出"**：在指定的输出目录下生成 `schlib/` 和 `pcblib/` 文件夹

### 剪贴板自动读取

勾选"剪贴板自动读取"后：
- 程序每 400ms 检查一次剪贴板内容
- 检测到新内容时自动执行搜索
- 将搜索结果的第一个元件自动添加到导出列表
- 适用于批量复制 LCSC 编号的场景

### 命令行使用

```bash
# 搜索元件
npnp search STM32 --limit 20

# 下载 3D 模型
npnp download C12702 --format step

# 导出单个元件库
npnp export C12702 --step

# 批量导出
npnp batch --input components.txt --output ./output --merge --library-name MyLibrary
```

components.txt 文件格式（每行一个 LCSC 编号）：

```
C12702
C529330
C2040
```

## 🏗️ 项目结构

```
src/
├── Npnp.Core/          # 核心类库：LCSC API、导出服务、Altium 库写入器
├── Npnp.CLI/           # 命令行工具（基于 Spectre.Console）
└── Transform.App/      # WPF 图形界面应用（MVVM 架构）
```

## 🛠️ 技术栈

- **框架**：.NET 8.0 + WPF
- **架构**：MVVM（CommunityToolkit.Mvvm）
- **依赖注入**：Microsoft.Extensions.DependencyInjection
- **命令行**：Spectre.Console.Cli
- **日志**：Serilog
- **外部工具**：[npnp](https://github.com/linkyourbin/npnp)（Rust 实现的 LCSC 元件库导出器）

## 📝 说明

- 本项目的核心功能依赖于 [npnp](https://github.com/linkyourbin/npnp) 提供的官方导出引擎
- 导出格式为 Altium Designer 原生格式（.SchLib / .PcbLib）
- 使用本工具需要能够访问 LCSC（立创商城）API 服务

## 📄 许可证

MIT License
