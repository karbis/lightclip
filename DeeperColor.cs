using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Reflection;

// copied from here https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.effects.shadereffect
// remaps 16-235 rgb to 0-255
// you lose color precision but whatever
namespace lightclip {
	public class DeeperColorEffect : ShaderEffect {
		private static PixelShader _pixelShader =
			new PixelShader() { UriSource = new Uri("pack://application:,,,/Properties/DeeperColor.ps") };

		public DeeperColorEffect() {
			PixelShader = _pixelShader;

			UpdateShaderValue(InputProperty);
		}

		public Brush Input {
			get { return (Brush)GetValue(InputProperty); }
			set { SetValue(InputProperty, value); }
		}

		public static readonly DependencyProperty InputProperty =
			ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(DeeperColorEffect), 0);

	}
}