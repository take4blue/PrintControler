using System.Windows;
using System.Windows.Controls;

namespace Take4.Common {
	/// <summary>
	/// SpeedText.xaml の相互作用ロジック
	/// </summary>
	public partial class SpeedText : UserControl {
		public SpeedText() {
			InitializeComponent();
		}

		public static DependencyProperty SpeedProperty =
		DependencyProperty.Register(
			"Speed",
			typeof(int),
			typeof(SpeedText));

		public int Speed {
			get => (int)GetValue(SpeedProperty);
			set { SetValue(SpeedProperty, value); }
		}
	}
}
