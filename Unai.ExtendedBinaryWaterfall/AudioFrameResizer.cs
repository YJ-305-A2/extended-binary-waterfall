using System;

namespace Unai.ExtendedBinaryWaterfall;

public class AudioFrameResizer<T>
{
	private T[] _outputBuffer = null;
	private int _bufOfs = 0;
	
	public int BufferLength
	{
		get => _outputBuffer.Length;
		set => _outputBuffer = new T[value];
	}
	public Action<T[]> OutputCallback { get; set; } = null;

	public void Push(T[] input)
	{
		var newBufOfs = _bufOfs + input.Length;
		if (newBufOfs >= BufferLength)
		{
			int inputOfs = 0;
			int outputOfs = _bufOfs;
			while (inputOfs < input.Length)
			{
				int subBufSize = BufferLength - outputOfs;
				if (inputOfs + subBufSize >= input.Length)
				{
					subBufSize = input.Length - inputOfs;
				}
				Logger.Trace($"音频缓冲区重排：{subBufSize} 字节，{inputOfs}–{inputOfs + subBufSize}/{input.Length} → {outputOfs}-{outputOfs + subBufSize}/{BufferLength}");
				Array.Copy(input, inputOfs, _outputBuffer, outputOfs, subBufSize);
				inputOfs += subBufSize;
				outputOfs += subBufSize;
				outputOfs %= BufferLength;

				if (inputOfs < input.Length)
				{
					Logger.Trace($"发送输出缓冲区…");
					OutputCallback?.Invoke(_outputBuffer);
				}
			}

			_bufOfs = outputOfs;
			if (_bufOfs > BufferLength)
			{
				Logger.Warning($"缓冲区溢出 {_bufOfs} > {BufferLength}");
				_bufOfs %= BufferLength;
			}
		}
		else
		{
			Array.Copy(input, 0, _outputBuffer, _bufOfs, input.Length);
			_bufOfs += input.Length;
		}
		Logger.Trace($"音频缓冲区状态：已填充{_bufOfs,4}/{BufferLength,4}，剩余{BufferLength - _bufOfs}字节");
	}
}
