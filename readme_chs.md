# 扩展二进制瀑布流

[English](readme.md)  
[简体中文](readme_chs.md)

[![最新标签](https://img.shields.io/github/v/tag/unai-d/extended-binary-waterfall?style=flat-square&label=最新标签)](https://github.com/unai-d/extended-binary-waterfall/tags)

本程序将计算机文件读取为原始音频和视频流，生成有时被称为"二进制瀑布流"的效果。"扩展"部分是指包含了对目标文件可能包含的片段、数据块或子文件的详细分析。

> [!WARNING]
> 本程序仍在开发中。  
> 部分代码尚未经过测试，运行此软件时可能会出现错误。

## 依赖项

### 必需
- .NET 9/10 SDK  
	- 虽然未在旧版本上进行测试，但可能与它们兼容。您可以在`csproj`文件中手动降低版本。

### 可选
- FFmpeg 库（用于 FFmpeg 导出器）  
	- 在 Windows 10/11 中，使用以下命令安装 FFmpeg：
		```powershell
		winget install "FFmpeg (Shared)"
		```
		安装完成后，重启命令行并确保`PATH`环境变量已更新为包含 FFmpeg 库的路径。  
	- 或者，您可以从 [CODEX FFMPEG](https://www.gyan.dev/ffmpeg/builds/) 手动下载库（确保下载"shared"版本）。  
	下载后，将 DLL 文件移动到已知路径（例如`C:\ffmpeg`）。

- [Unifont](https://unifoundry.com/unifont/index.html)  
	- 某些 Linux 发行版可以通过各自的包管理器安装此字体，但在 Windows 中需要手动下载。  
	- 请确保安装默认字体和用于表情符号的"upper"变体。

- `[wimlib](https://wimlib.net/)` 用于 WIM 文件列表。

- `minidump` Python 模块用于 Windows Minidump 内存区域解析。

> [!IMPORTANT]
> 使用 Unifont 时，请确保安装 TTF 格式而非 OTF。  
> 似乎 Unifont 包含的 OTF CFF2 表格会导致文本渲染库抛出异常。  
> 由于 Unifoundry 不再正式发布 TTF 版本，您可以从[非官方仓库](https://github.com/multitheftauto/unifont)下载 TTF 版本。  
> 有关此特定问题，请参阅 SixLabors Fonts 的相关[问题](https://github.com/SixLabors/Fonts/issues/331)和[拉取请求](https://github.com/SixLabors/Fonts/pull/342)。

## 构建和运行

使用 `run.sh` 可以快速（如有必要先构建，然后）运行程序。  
或者，可以使用标准的 `dotnet build`/`dotnet run` 命令：  
从仓库路径使用 `dotnet build` 命令，然后执行 `dotnet run --project Unai.ExtendedBinaryWaterfall.Cli` 运行程序。

## 使用方法

### 快速入门

以下命令假设您的命令行工作目录位于构建过程生成的二进制文件所在位置。  
如果您使用 Windows，可执行文件将带有 `.exe` 后缀。  
执行以下命令获取可用参数的信息：
```sh
Unai.ExtendedBinaryWaterfall.Cli --help
```
当使用 `run.sh` 时，命令可以简化为：
```sh
./run.sh --help
```

### 示例

**示例 1**  
读取 ISO 文件并将结果保存到名为 `result.mkv` 的视频文件中（需要 FFmpeg）：
```sh
Unai.ExtendedBinaryWaterfall.Cli /path/to/file.iso --exporter=ffmpeg --output=result.mkv
```

**示例 2**  
读取 GameMaker 归档文件并在 SDL 窗口中预览结果：
```sh
Unai.ExtendedBinaryWaterfall.Cli /path/to/data.win
```
如果未指定导出器，SDL 是默认导出器。

**示例 3**  
读取 `.dll` 文件并通过标准输出重定向预览 FFmpeg 编码结果：
```sh
Unai.ExtendedBinaryWaterfall.Cli "C:\Windows\system32\shell32.dll" --exporter=ffmpeg | ffplay -f matroska -
```
当未指定 `-o`/`--output` 参数时，EBW 将默认使用标准输出。

> [!WARNING]
> 某些命令行界面（如 PowerShell）需要合适的标准 I/O 编码以处理二进制流。  
> 否则您将得到"损坏"的文件。