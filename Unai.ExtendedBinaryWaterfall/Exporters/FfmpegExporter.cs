using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

[Exporter("ffmpeg", "FFmpeg流", "使用FFmpeg库编码音视频数据并以Matroska格式输出")]
public class FfmpegExporter : IExporter
{
	private bool _init = false;

	private unsafe AVFormatContext* _fmtCtx;

	private unsafe AVStream* _videoStream;
	private unsafe AVCodecContext* _videoCtx;
	private unsafe AVFrame* _videoAvFrame;
	private unsafe AVFrame* _videoAvFramePre;
	private unsafe AVPacket* _videoAvPacket;

	private unsafe AVStream* _audioStream;
	private unsafe AVCodecContext* _audioCtx;
	private unsafe AVFrame* _audioAvFrame;
	private unsafe AVPacket* _audioAvPacket;

	private unsafe SwsContext* _swsCtx;

	private readonly AudioFrameResizer<float> _audioQueue = new();

	private int _frameNum = 0;

	public Generator Generator { get; set; }

	#region User-defined properties

	[CliParameter("FFmpeg日志级别", "ffloglevel")]
	public int LogLevel { get; set; } = ffmpeg.AV_LOG_INFO;
	// [CliParameter("Output Video File Path", "output", 'o')]
	// public string OutputPath { get; set; } = null;
	[CliParameter("输出视频比特率", "output-bitrate")]
	public uint OutputVideoBitRate { get; set; } = 9_000_000;

	#endregion

	public void InitializeFfmpeg()
	{
		unsafe
		{
			ffmpeg.RootPath = FfmpegUtils.GetFfmpegLibraryPath();
			Logger.Debug($"FFmpeg库路径：'{ffmpeg.RootPath}'");

			ffmpeg.av_log_set_level(LogLevel);
			av_log_set_callback_callback logCb = (p0, level, format, v1) =>
			{
				if (level > ffmpeg.av_log_get_level()) return;
				var messageBufferLen = 1024; // is this too much for the stack?
				var messageBuffer = stackalloc byte[messageBufferLen];
				var printPrefix = 1;
				ffmpeg.av_log_format_line(p0, level, format, v1, messageBuffer, messageBufferLen, &printPrefix);
				var message = Marshal.PtrToStringAnsi((nint)messageBuffer);
				Console.Error.Write(message);
			};
			ffmpeg.av_log_set_callback(logCb);

			// format
			// ======

			{
				AVFormatContext* fmtCtx = null;
				ffmpeg.avformat_alloc_output_context2(&fmtCtx, null, "matroska", Generator.OutputFilePath ?? "/dev/stdout");
				if (fmtCtx == null) Console.Error.WriteLine("无法分配AVFormatContext");
				_fmtCtx = fmtCtx;
			}
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				Logger.Debug("格式要求全局流头信息");
			}

			// encoders
			// ========

			AVRational videoFps; videoFps.num = Generator.OutputFps; videoFps.den = 1;

			var videoEnc = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
			var audioEnc = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);

			_videoCtx = ffmpeg.avcodec_alloc_context3(videoEnc);
			_videoCtx->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
			_videoCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
			_videoCtx->width = 1920;
			_videoCtx->height = 1080;
			_videoCtx->time_base.num = 1;
			_videoCtx->time_base.den = videoFps.num;
			_videoCtx->framerate.num = videoFps.num;
			_videoCtx->framerate.den = videoFps.den;
			_videoCtx->bit_rate = OutputVideoBitRate;
			// _videoCtx->thread_count = Environment.ProcessorCount / 2;
			// Console.Error.WriteLine($"using {_videoCtx->thread_count} threads");
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				_videoCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
			}
			// ffmpeg.av_opt_set(_videoCtx->priv_data, "crf", "23", 0);
			// h264 codec fails with EINVAL/11 if extradata does not get allocated manually.
			if (_videoCtx->codec->id == AVCodecID.AV_CODEC_ID_H264)
			{
				_videoCtx->extradata = (byte*)ffmpeg.av_malloc(32);
				_videoCtx->extradata_size = 24;
			}
			AVDictionary* videoEncOpts;
			var ret = ffmpeg.avcodec_open2(_videoCtx, videoEnc, &videoEncOpts);
			FfmpegUtils.LogIfAvError(ret, "无法打开视频编码器");

			_audioCtx = ffmpeg.avcodec_alloc_context3(audioEnc);
			_audioCtx->codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO;
			_audioCtx->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
			_audioCtx->sample_rate = Generator.AudioOutputSampleRate;
			_audioCtx->time_base.num = 1;
			_audioCtx->time_base.den = Generator.AudioOutputSampleRate;
			_audioCtx->ch_layout.nb_channels = 2;
			_audioCtx->ch_layout.order = AVChannelOrder.AV_CHANNEL_ORDER_NATIVE;
			_audioCtx->ch_layout.u.mask = ffmpeg.AV_CH_LAYOUT_STEREO;
			_audioCtx->bit_rate = 128_000;
			_audioCtx->extradata = (byte*)ffmpeg.av_mallocz(32);
			_audioCtx->extradata_size = 24;
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				_audioCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
			}
			ret = ffmpeg.avcodec_open2(_audioCtx, audioEnc, null);
			FfmpegUtils.LogIfAvError(ret, "无法打开视频编码器");

			// streams
			// =======

			_videoStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
			if (_videoStream == null) Logger.Error("无法分配视频输出流");
			_videoStream->index = (int)(_fmtCtx->nb_streams - 1);
			_videoStream->time_base = _videoCtx->time_base;
			_videoStream->r_frame_rate = videoFps;

			ret = ffmpeg.avcodec_parameters_from_context(_videoStream->codecpar, _videoCtx);
			FfmpegUtils.LogIfAvError(ret, "无法从编码器上下文设置视频编解码参数");

			_audioStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
			if (_videoStream == null) Logger.Error("无法分配音频输出流");
			_audioStream->index = (int)(_fmtCtx->nb_streams - 1);
			_audioStream->time_base = FfmpegUtils.GetRational(1, _audioCtx->sample_rate);

			ret = ffmpeg.avcodec_parameters_from_context(_audioStream->codecpar, _audioCtx);
			FfmpegUtils.LogIfAvError(ret, "无法从编码器上下文设置音频编解码参数");
			if (_audioStream->codecpar->extradata == null)
			{
				Logger.Error("音频编码器未创建extradata缓冲区");
			}

			// output file/stream
			// ==================

			ret = ffmpeg.avio_open(&_fmtCtx->pb, Generator.OutputFilePath ?? "pipe:", Generator.OutputFilePath != null ? ffmpeg.AVIO_FLAG_READ_WRITE : ffmpeg.AVIO_FLAG_WRITE);
			FfmpegUtils.LogIfAvError(ret, "无法打开标准输出");
			AVDictionary* fmtOpts;
			ret = ffmpeg.avformat_write_header(_fmtCtx, &fmtOpts);
			FfmpegUtils.LogIfAvError(ret, "无法写入头信息");

			byte* dictBuf = (byte*)ffmpeg.av_malloc(1024);
			ffmpeg.av_dict_get_string(fmtOpts, &dictBuf, (byte)'=', (byte)':');

			// video frames
			// ============

			_videoAvFrame = ffmpeg.av_frame_alloc();
			_videoAvFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
			_videoAvFrame->width = 1920;
			_videoAvFrame->height = 1080;
			_videoAvFrame->time_base = _videoStream->time_base;

			ret = ffmpeg.av_frame_get_buffer(_videoAvFrame, 0);
			FfmpegUtils.LogIfAvError(ret, "无法分配视频像素缓冲区");

			_videoAvFramePre = ffmpeg.av_frame_alloc();
			_videoAvFramePre->format = (int)AVPixelFormat.AV_PIX_FMT_RGBA;
			_videoAvFramePre->width = 1920;
			_videoAvFramePre->height = 1080;
			_videoAvFramePre->time_base = _videoStream->time_base;

			ret = ffmpeg.av_frame_get_buffer(_videoAvFramePre, 0);
			FfmpegUtils.LogIfAvError(ret, "无法分配视频像素缓冲区");

			// audio frames
			// ============

			_audioAvFrame = ffmpeg.av_frame_alloc();
			_audioAvFrame->format = (int)AVSampleFormat.AV_SAMPLE_FMT_FLTP;
			ffmpeg.av_channel_layout_copy(&_audioAvFrame->ch_layout, &_audioCtx->ch_layout);
			_audioAvFrame->sample_rate = _audioCtx->sample_rate;
			_audioAvFrame->nb_samples = _audioCtx->frame_size;
			_audioAvFrame->ch_layout.nb_channels = 2;
			_audioAvFrame->ch_layout.u.mask = 3;
			_audioAvFrame->time_base.num = _audioCtx->time_base.num;
			_audioAvFrame->time_base.den = _audioCtx->time_base.den;

			if ((_audioCtx->codec->capabilities & ffmpeg.AV_CODEC_CAP_VARIABLE_FRAME_SIZE) == 0)
			{
				Logger.Warning("音频编码器不支持可变帧大小");
			}

			ret = ffmpeg.av_frame_get_buffer(_audioAvFrame, 0);
			FfmpegUtils.LogIfAvError(ret, "无法分配音频采样缓冲区");
			_audioQueue.BufferLength = _audioAvFrame->nb_samples * _audioAvFrame->ch_layout.nb_channels;
			_audioQueue.OutputCallback = (buf) =>
			{
				float* ab0 = (float*)_audioAvFrame->data[0];
				float* ab1 = (float*)_audioAvFrame->data[1];
				for (int i = 0; i < _audioAvFrame->linesize[0] / sizeof(float); i++)
				{
					ab0[i] = buf[i * 2];
					ab1[i] = buf[i * 2 + 1];
				}
				DoEncode(_audioCtx, _audioStream, _audioAvFrame, _audioAvPacket);
			};

			Logger.Debug($"视频原始行大小 = {_videoAvFramePre->linesize[0]} {_videoAvFramePre->linesize[1]}");
			Logger.Debug($"视频目标行大小 = {_videoAvFrame->linesize[0]} {_videoAvFrame->linesize[1]} {_videoAvFrame->linesize[2]}");
			Logger.Debug($"所需音频帧大小 = {_audioCtx->frame_size} * {_audioCtx->ch_layout.nb_channels}声道");
			Logger.Debug($"音频声道布局 =   {_audioAvFrame->ch_layout.nb_channels}声道 {_audioAvFrame->ch_layout.order}顺序 掩码{_audioAvFrame->ch_layout.u.mask}");
			Logger.Debug($"音频行大小 =     {_audioAvFrame->linesize[0]} {_audioAvFrame->linesize[1]} {_audioAvFrame->linesize[2]} {_audioAvFrame->linesize[3]} {_audioAvFrame->linesize[4]} {_audioAvFrame->linesize[5]} {_audioAvFrame->linesize[6]} {_audioAvFrame->linesize[7]}");

			_videoAvPacket = ffmpeg.av_packet_alloc();
			_audioAvPacket = ffmpeg.av_packet_alloc();

			ffmpeg.av_dump_format(_fmtCtx, 0, Generator.OutputFilePath ?? "pipe:", 1);
		}
		_init = true;
	}

	private unsafe void DoEncode(AVCodecContext* cCtx, AVStream* stream, AVFrame* frame, AVPacket* packet)
	{
		int ret;

		FfmpegUtils.LogFrameData(frame);

		ret = ffmpeg.avcodec_send_frame(cCtx, frame);
		FfmpegUtils.LogIfAvError(ret, "无法发送帧到编码器");

		while (ret >= 0)
		{
			ret = ffmpeg.avcodec_receive_packet(cCtx, packet);

			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
			{
				break;
			}
			else if (ret < 0)
			{
				// if frame is null, `EOF` code is expected, don't treat it as an error.
				if (frame != null)
				{
					FfmpegUtils.LogIfAvError(ret, "无法编码");
				}
				break;
			}

			ffmpeg.av_packet_rescale_ts(packet, cCtx->time_base, stream->time_base);
			packet->stream_index = stream->index;
			packet->time_base.num = stream->time_base.num;
			packet->time_base.den = stream->time_base.den;
			FfmpegUtils.LogPacketData(packet);

			ret = ffmpeg.av_interleaved_write_frame(_fmtCtx, packet);
			FfmpegUtils.LogIfAvError(ret, "无法写入数据包");
			if (ret == -32) Generator._exitRequested = true;
		}
	}

	public void PushNewFrame(Image videoFrame, AudioBuffer audioFrame, double delta)
	{
		PushNewFrame((Image<Rgba32>)videoFrame, audioFrame, delta);
	}

	public unsafe void PushNewFrame(Image<Rgba32> videoFrame, AudioBuffer audioFrame, double delta)
	{
		if (!_init)
		{
			InitializeFfmpeg();
		}

		var ret = ffmpeg.av_frame_make_writable(_videoAvFrame);
		FfmpegUtils.LogIfAvError(ret, "无法使视频像素数据可写");
		ret = ffmpeg.av_frame_make_writable(_audioAvFrame);
		FfmpegUtils.LogIfAvError(ret, "无法使音频采样缓冲区可写");

		_audioAvFrame->pts = (long)(_audioAvFrame->sample_rate * (_frameNum / (float)Generator.OutputFps));
		_audioAvFrame->duration = Generator.AudioOutputSamplesPerFrame;

		// TODO: move to init method
		if (_swsCtx == null)
		{
			_swsCtx = ffmpeg.sws_getContext(videoFrame.Width, videoFrame.Height, (AVPixelFormat)_videoAvFramePre->format, videoFrame.Width, videoFrame.Height, (AVPixelFormat)_videoAvFrame->format, ffmpeg.SWS_BILINEAR, null, null, null);
			if (_swsCtx == null)
			{
				Logger.Error("无法初始化sws上下文");
			}
		}

		if (_swsCtx != null)
		{
			var pixelData = new byte[videoFrame.Width * videoFrame.Height * 4];
			videoFrame.CopyPixelDataTo(pixelData);

			Marshal.Copy(pixelData, 0, (nint)_videoAvFramePre->data[0], pixelData.Length);
			ffmpeg.sws_scale(_swsCtx, _videoAvFramePre->data, _videoAvFramePre->linesize, 0, _videoAvFramePre->height, _videoAvFrame->data, _videoAvFrame->linesize);
		}
		else if (_videoAvFrame->format == (int)AVPixelFormat.AV_PIX_FMT_GBRP)
		{
			// Unoptimized pixel copy.
			videoFrame.ProcessPixelRows((pa) =>
			{
				for (int y = 0; y < pa.Height; y++)
				{
					var row = pa.GetRowSpan(y);

					for (int x = 0; x < pa.Width; x++)
					{
						var p = row[x];
						_videoAvFrame->data[0][_videoAvFrame->linesize[0] * y + x] = p.G;
						_videoAvFrame->data[1][_videoAvFrame->linesize[1] * y + x] = p.B;
						_videoAvFrame->data[2][_videoAvFrame->linesize[2] * y + x] = p.R;
					}
				}
			});
		}

		_videoAvFrame->time_base.num = _videoCtx->time_base.num;
		_videoAvFrame->time_base.den = _videoCtx->time_base.den;
		_videoAvFrame->pts = _frameNum;
		_videoAvFrame->duration = 1;
		DoEncode(_videoCtx, _videoStream, _videoAvFrame, _videoAvPacket);

		_audioQueue.Push(audioFrame.ToArray());

		if (_frameNum % 10 == 0)
		{
			Logger.Trace($"帧 {_frameNum}，时间戳 {_frameNum / Generator.OutputFps}，生成速度 {(int)(1 / delta)}FPS\x1b[K\x1b[G");
		}

		_frameNum++;
	}

	public unsafe void Finish()
	{
		Logger.Debug("正在刷新流…");
		DoEncode(_videoCtx, _videoStream, null, _videoAvPacket);
		DoEncode(_audioCtx, _audioStream, null, _audioAvPacket);

		Logger.Debug("正在释放FFmpeg资源…");
		ffmpeg.sws_freeContext(_swsCtx);
		var videoCtx = _videoCtx;
		ffmpeg.avcodec_free_context(&videoCtx);
		var audioCtx = _audioCtx;
		ffmpeg.avcodec_free_context(&audioCtx);

		ffmpeg.av_write_trailer(_fmtCtx);

		if ((_fmtCtx->flags & ffmpeg.AVFMT_NOFILE) == 0)
		{
			ffmpeg.avio_closep(&_fmtCtx->pb);
		}

		ffmpeg.avformat_free_context(_fmtCtx);
	}
}
