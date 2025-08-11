# 语言
- [English](readme.md)
- [Chinese (Simplified)](readme_chs.md)

# Extended Binary Waterfall（扩展二进制瀑布）
![GitHub Tag](https://img.shields.io/github/v/tag/unai-d/extended-binary-waterfall?style=flat-square&label=latest%20tag)

本程序可将**任意计算机文件**作为**原始音频**和**视频流**读取，生成所谓的**二进制瀑布**。其“扩展”特性体现在对目标文件中可能存在的**片段、块或子文件**进行详细解析。

> [!WARNING]
> 本程序仍处于开发阶段。
> 部分代码尚未经过完整测试，运行过程中可能发生错误。

## 依赖项

- 必需
	- .NET 9 SDK
		- 未在旧版本测试，但**可能兼容**。可手动修改 `csproj` 文件降低版本要求。
- 可选
	- FFmpeg 库（用于 FFmpeg 导出器）
		- Windows 10/11 安装命令：
			```powershell
			winget install "FFmpeg (Shared)"
			```
			安装后**重启命令行**并确保 `PATH` 环境变量包含 FFmpeg 库路径。
		- 或手动下载[CODEX FFMPEG](https://www.gyan.dev/ffmpeg/builds/)（选择“shared”版本），将 DLL 文件放置于指定路径（如 `C:\ffmpeg`）。
	- [Unifont](https://unifoundry.com/unifont/index.html)
		- Linux 可通过包管理器安装，Windows 需手动下载。
		- 需同时安装默认字体和表情符号的“upper”变体。
	- [`wimlib`](https://wimlib.net/)（用于解析 WIM 文件结构）
	- `minidump` Python 模块（用于解析 Windows Minidump 内存区域）

> [!IMPORTANT]
> 当使用Unifont时，请确保安装**TTF格式**而非OTF格式。
> Unifont的OTF文件似乎包含CFF2表（Compact Font Format Version 2），这会导致文本渲染库抛出异常。
> 由于Unifoundry官方已**不再发布**TTF格式文件，您可以从[**非官方仓库**](https://github.com/multitheftauto/unifont)下载TTF版本。
> 建议查看与SixLabors.Fonts相关的议题和拉取请求（[SixLabors.Fonts issue](https://github.com/SixLabors/Fonts/issues/331) | [相关PR](https://github.com/SixLabors/Fonts/pull/342)）以了解此问题的具体细节。

## 构建与运行

使用 `run.sh` 快速（必要时构建并）运行程序。

或使用标准命令：
```sh
dotnet build
dotnet run --project Unai.ExtendedBinaryWaterfall.Cli
```

## 使用指南

### 快速开始

以下命令假设工作目录为构建生成的二进制文件路径。Windows 下可执行文件后缀为 `.exe`。

查看帮助信息：

```sh
Unai.ExtendedBinaryWaterfall.Cli --help
```

使用 `run.sh` 时简化为：

```sh
./run.sh --help
```

### 示例

#### 示例 1

读取 ISO 文件并导出为视频 `result.mkv`（需 FFmpeg）：

```sh
Unai.ExtendedBinaryWaterfall.Cli /path/to/file.iso --exporter=ffmpeg --output=result.mkv
```

#### 示例 2

读取 GameMaker 存档文件并在 SDL 窗口预览（默认导出器）：

```sh
Unai.ExtendedBinaryWaterfall.Cli /path/to/data.win
```

若未指定导出器，SDL 将作为默认导出器。

#### 示例 3

读取 `.exe` 文件并通过标准输出预览 FFmpeg 编码结果：

```sh
Unai.ExtendedBinaryWaterfall.Cli "C:\Windows\system32\shell32.dll" --exporter=ffmpeg | ffplay -f matroska -
```

当未指定 `-o`/`--output` 参数时，EBW 将默认输出至标准输出。

> [!WARNING]
> PowerShell 等命令行工具需确保标准输入输出编码兼容二进制流，否则可能会导致文件损坏。
