using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace InkTestWpf
{
  /// <summary>
  /// Window1.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingWindow : Window
  {
    public SettingWindow()
    {
      InitializeComponent();
      BorderValue.Text = InkTestWpf.Properties.Settings.Default.Border.ToString();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      int value;
      if (int.TryParse(BorderValue.Text, out value) && value > 0)
      {
        InkTestWpf.Properties.Settings.Default.Border = value;
        this.Close();
      }
    }
  }
}
