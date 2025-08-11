using SixLabors.ImageSharp;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

[Exporter("null", "空/虚拟输出", "不处理生成的视频。用于调试目的。")]
public class NullExporter : IExporter
{
	public Generator Generator { get; set; }

	public void Finish()
	{
		
	}

	public void PushNewFrame(Image videoFrame, AudioBuffer audioFrame, double delta = 0.04)
	{
		
	}
}
