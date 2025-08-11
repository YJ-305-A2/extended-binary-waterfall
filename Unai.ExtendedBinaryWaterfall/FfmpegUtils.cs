using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace Unai.ExtendedBinaryWaterfall;

public static class FfmpegUtils
{
	private static readonly string[] _ffmpegSearchPathsLinux =
	[
		"/usr/lib",
		"/usr/lib64",
		"/usr/lib32",
		"/lib",
		"/lib64",
		"/lib32"
	];

	private static readonly string[] _ffmpegSearchPathsWindows =
	[
		"C:\\ffmpeg"
	];

	public static string GetFfmpegLibraryPath()
	{
		Logger.Debug("正在推测FFmpeg库路径…");
		IEnumerable<string> ret;

		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			ret = _ffmpegSearchPathsWindows
				.Where(Directory.Exists)
				.Where(x => Directory.GetFiles(x, "*avcodec-*.dll").Length > 0);
		}
		else
		{
			ret = _ffmpegSearchPathsLinux
				.Where(Directory.Exists)
				.Where(x => File.Exists($"{x}/libavcodec.so"));
		}
		
		Logger.Debug($"检测到 {ret.Count()} 条库路径。");
		if (ret.Any())
		{
			return ret.FirstOrDefault();
		}

		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			Logger.Debug("通过预定义路径搜索失败。正在尝试PATH环境变量…");
			ret = Environment.GetEnvironmentVariable("PATH")
				.Split(';')
				.Where(Directory.Exists)
				.Where(p => Directory.GetFiles(p, "avcodec*.dll").Length > 0);

			if (ret.Any())
			{
				return ret.FirstOrDefault();
			}
		}

		Logger.Error("无法确定包含FFmpeg库的文件夹路径。");
		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			Logger.Info("请执行以下命令安装FFmpeg库：");
			Logger.Info("	winget install \"FFmpeg (Shared)\"");
			Logger.Info("安装完成后请重启命令行。");
			Logger.Info("或手动下载FFmpeg库并保存到以下位置：");
			Logger.Info($"	{_ffmpegSearchPathsWindows[0]}");
			Logger.Info("注意：这些库文件名可能以 `libav` 或 `av` 开头（例如：`avcodec-61.dll`）。");
		}
		return null;
	}

	public unsafe static void LogIfAvError(int errorCode, string message)
	{
		if (errorCode < 0)
		{
			byte* errbuf = (byte*)ffmpeg.av_malloc(1024);
			ffmpeg.av_make_error_string(errbuf, 1024, errorCode);
			Logger.Error($"FFmpeg错误 {errorCode}: {message}: {Marshal.PtrToStringUTF8((nint)errbuf)}");
			ffmpeg.av_free(errbuf);
		}
	}

	public static AVRational GetRational(int num, int den)
	{
		AVRational ret;
		ret.num = num;
		ret.den = den;
		return ret;
	}

	public unsafe static void LogFrameData(AVFrame* frame)
	{
		if (frame != null)
		{
			Logger.Trace($"帧：pts={frame->pts} dur={frame->duration} tb={frame->time_base.num}/{frame->time_base.den}");
		}
	}

	public unsafe static void LogPacketData(AVPacket* packet)
	{
		Logger.Trace($"数据包：str={packet->stream_index} pts={packet->pts} dts={packet->dts} dur={packet->duration} tb={packet->time_base.num}/{packet->time_base.den}");
	}
}
