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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Ink;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Win32;
using System.Xml;
using System.Xml.Schema;

namespace InkTestWpf
{
  /// <summary>
  /// MainWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class MainWindow : Window
  {
    public class NoteData
    {
      public List<string> TBtexts = new List<string>();
      public List<double> TBXs = new List<double>();
      public List<double> TBYs = new List<double>();
      public List<double> TBFontSizes = new List<double>();
      public List<double> TBLineHeights = new List<double>();

      public byte[] tegaki { set; get; }
      public int borderMargin { set; get; }
      public NoteData() { }
    }
    InkAnalyzer ia = new InkAnalyzer();
    int lineMargine;
    int lastSelectedTab;

    MenuItem Copy = new MenuItem() { Header = "コピー" };
    MenuItem Cut = new MenuItem() { Header = "切り取り" };
    MenuItem Paste = new MenuItem() { Header = "貼り付け" };
    MenuItem Remove = new MenuItem() { Header = "削除" };
    MenuItem OCR = new MenuItem() { Header = "テキストに変換" };

    ContextMenu NormalMenu = new ContextMenu();
    ContextMenu EditMenu = new ContextMenu();

    //List<NoteData> Pages = new List<NoteData>();
    Dictionary<string, NoteData> Pages = new Dictionary<string, NoteData>();
    bool tabDeleted;

    public MainWindow()
    {
      NormalMenu.Items.Add(Paste);
      EditMenu.Items.Add(Copy);
      EditMenu.Items.Add(Cut);
      EditMenu.Items.Add(Remove);
      EditMenu.Items.Add(OCR);

      Copy.Click += Copy_Click;
      Cut.Click += Cut_Click;
      Paste.Click += Paste_Click;
      Remove.Click += Remove_Click;
      OCR.Click += OCR_Click;

      InkTestWpf.Properties.Settings.Default.Reload();
      lineMargine = InkTestWpf.Properties.Settings.Default.Border;
      InitializeComponent();
      InkRecognizerCollection recognizerList = ia.GetInkRecognizersByPriority();
      foreach (InkRecognizer r in recognizerList)
      {
        EngineList.Items.Add(r);
      }
      AddAddTabButton();
      TabAddButton_Click(null, null);//1ページ目
      lastSelectedTab = 1;
      TabControl1.SelectedIndex = 1;
    }

    //選択したストロークを文字列に変換
    void OCR_Click(object sender, RoutedEventArgs e)
    {
      OCRText(1);
    }

    //使わないかも
    InkCanvas GetTopCanvas()
    {
      int maxZ = -10;
      InkCanvas topIC = null;
      foreach (InkCanvas IC in noteRoot.Children.OfType<InkCanvas>())
      {
        if (Panel.GetZIndex(IC) > maxZ)
        {
          maxZ = Panel.GetZIndex(IC);
          topIC = IC;
        }
      }
      return topIC;
    }

    void Paste_Click(object sender, RoutedEventArgs e)
    {
      TegakiCanvas.Paste(PointFromScreen(new System.Windows.Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y - 100)));
    }

    void Copy_Click(object sender, RoutedEventArgs e)
    {
      TegakiCanvas.CopySelection();
    }

    void Cut_Click(object sender, RoutedEventArgs e)
    {
      Copy_Click(null, null);
      Remove_Click(null, null);
      TegakiCanvas.ContextMenu = NormalMenu;
    }

    //削除ボタン
    void Remove_Click(object sender, RoutedEventArgs e)
    {
      List<UIElement> UIEL = new List<UIElement>();
      List<Stroke> SL = TegakiCanvas.GetSelectedStrokes().ToList();
      foreach (UIElement UIE in TegakiCanvas.GetSelectedElements())
      {
        UIEL.Add(UIE);
      }
      foreach (UIElement UIE in UIEL)
      {
        TegakiCanvas.Children.Remove(UIE);
      }
      foreach (Stroke S in SL)
      {
        TegakiCanvas.Strokes.Remove(S);
      }
      TegakiCanvas.ContextMenu = NormalMenu;
    }

    private void getStrokeZahyo(StrokeCollection sc, out double x, out double y, out int height)
    {
      x = 10000;
      y = 10000;
      height = 0;
      foreach (Stroke s in sc)
      {
        if (s.GetBounds().X < x) x = s.GetBounds().X;
        if (s.GetBounds().Y < y) y = s.GetBounds().Y;
        if (s.GetBounds().Height > height) height = (int)s.GetBounds().Height;
      }
    }
    private void ScrollToHalfVerticalOffsetButto_Click(object sender, RoutedEventArgs e)
    {
      this.scrollViewer.ScrollToVerticalOffset(this.scrollViewer.ScrollableHeight / 2);
    }

    private void DrawNoteLine(int interval)
    {
      for (int i = 0; i * interval < /*gridStack.ActualHeight*/2000; i++)
      {
        //<Line Stroke="red" X1="0" Y1="25" X2="{Binding ElementName=gridStack, Path=Width}" Y2="25" />
        Line l = new Line() { Stroke = Brushes.Gray, X1 = 0, Y1 = interval, Y2 = interval };
        l.SetBinding(
          Line.X2Property,
          new System.Windows.Data.Binding("Width")
          {
            Source = gridStack
          });
        gridStack.Children.Add(l);
      }
    }

    private void OCRCanvas_PreviewStylusUp(object sender, StylusEventArgs e)
    {
      OCRText();
    }

    private void OCRText(int mode = 0)
    {
      StrokeCollection sc;
      switch (mode)
      {
        case 0:
          sc = OCRCanvas.Strokes;
          break;
        case 1:
          sc = TegakiCanvas.GetSelectedStrokes();
          break;
        default:
          return;
      }

      ia = new InkAnalyzer();
      if (sc.Count == 0) return;
      CustomRecognizerNode node;
      double x, y;
      int height;

      getStrokeZahyo(sc, out x, out y, out height);

      // キャンバスに描かれた文字を認識するためにアナライザにストロークをセット
      if (EngineList.SelectedItem != null)
      {
        node = ia.CreateCustomRecognizer((Guid)EngineList.SelectedValue);
        ia.AddStrokesToCustomRecognizer(sc, node);
      }
      else
      {
        ia.AddStrokes(sc);
      }
      ia.SetStrokesType(sc, StrokeType.Writing);

      // 文字を解析
      ia.Analyze();

      //罫線にピタッとなるようにする
      y = ((int)((y + lineMargine / 2) / lineMargine) * lineMargine) + 1 * (int)((y + lineMargine / 2) / lineMargine);

      height = (int)((height + lineMargine / 2) / lineMargine) * lineMargine;
      if (height >= 8)
      {
        TextBlock tb = new TextBlock() { Text = ia.GetRecognizedString(), FontSize = height - 8, VerticalAlignment = System.Windows.VerticalAlignment.Top, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, LineHeight = height };
        //noteRoot.Children.Add(tb);
        TegakiCanvas.Children.Add(tb);
        InkCanvas.SetLeft(tb, x);
        InkCanvas.SetTop(tb, y);
      }
      //textBox1.Text += ia.GetRecognizedString();
      switch (mode)
      {
        case 0:
          OCRCanvas.Strokes.Clear();
          break;
        case 1:
          foreach (Stroke s in sc)
          {
            TegakiCanvas.Strokes.Remove(s);
          }
          break;
      }
      /*
      // その他の候補を表示する
      AnalysisAlternateCollection alternates = theInkAnalyzer.GetAlternates();
      foreach (var alternate in alternates)
        MessageBox.Show(alternate.RecognizedString);
      */

    }

    private void OCRCanvas_PreviewStylusButtonDown(object sender, StylusButtonEventArgs e)
    {
      if (e.StylusButton.Name != "Barrel Switch") return;
      Panel.SetZIndex(TegakiCanvas, 10);
      TegakiCanvas.EditingMode = InkCanvasEditingMode.Select;
      TegakiCanvas.ContextMenu = NormalMenu;
    }

    private void OCRCanvas_PreviewStylusButtonUp(object sender, StylusButtonEventArgs e)
    {
      if (e.StylusButton.Name != "Barrel Switch") return;
      TegakiCanvas.EditingMode = InkCanvasEditingMode.Ink;
      if (OCRButton.IsChecked == true)
      {
        Panel.SetZIndex(TegakiCanvas, 0);
      }
    }

    private void TegakiCanvas_PreviewStylusButtonDown(object sender, StylusButtonEventArgs e)
    {
      if (e.StylusButton.Name != "Barrel Switch") return;
      TegakiCanvas.EditingMode = InkCanvasEditingMode.Select;
    }

    private void TegakiCanvas_PreviewStylusButtonUp(object sender, StylusButtonEventArgs e)
    {
      if (e.StylusButton.Name != "Barrel Switch") return;
      TegakiCanvas.EditingMode = InkCanvasEditingMode.Ink;
      if (OCRButton.IsChecked == true)
      {
        Panel.SetZIndex(TegakiCanvas, 0);
      }
    }

    private void root_Loaded(object sender, RoutedEventArgs e)
    {
      DrawNoteLine(lineMargine);
    }

    private void InkButton_Click(object sender, RoutedEventArgs e)
    {
      Panel.SetZIndex(TegakiCanvas, 0);
      OCRButton.IsChecked = true;
      TegakiButton.IsChecked = false;
    }

    private void TextButton_Checked(object sender, RoutedEventArgs e)
    {
      Panel.SetZIndex(TegakiCanvas, 10);
      OCRButton.IsChecked = false;
      TegakiButton.IsChecked = true;
    }

    //保存ボタン
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
      MemoryStream ms = new MemoryStream();
      TegakiCanvas.Strokes.Save(ms, true);
      NoteData nd = new NoteData() { tegaki = ms.ToArray(), borderMargin = lineMargine };
      foreach (TextBlock TB in TegakiCanvas.Children.OfType<TextBlock>())
      {
        nd.TBFontSizes.Add(TB.FontSize);
        nd.TBLineHeights.Add(TB.LineHeight);
        nd.TBtexts.Add(TB.Text);
        nd.TBXs.Add(InkCanvas.GetLeft(TB));
        nd.TBYs.Add(InkCanvas.GetTop(TB));
      }
      Pages[(((TabControl1.Items[TabControl1.SelectedIndex] as TabItem).Header as DockPanel).Children[1] as TextBlock).Text] = nd;


      SaveFileDialog sfd = new SaveFileDialog() { AddExtension = true, DefaultExt = "note" };
      sfd.ShowDialog();//sfd.FileName
      if (sfd.FileName == "") return;
      FileStream fs = new FileStream(sfd.FileName, FileMode.Create);

      List<byte[]> data = new List<byte[]>();

      foreach (KeyValuePair<string, NoteData> kvp in Pages)
      {
        ms = new MemoryStream();
        nd = kvp.Value;
        XmlSerializer xs = new XmlSerializer(typeof(NoteData));
        xs.Serialize(ms, nd);
        data.Add(ms.ToArray());
        ms.Dispose();
      }
      XmlSerializer XS = new XmlSerializer(typeof(List<byte[]>));
      XS.Serialize(fs, data);
      fs.Dispose();
      /* List<NoteData>版
      List<NoteData> data = new List<NoteData>();
      foreach (KeyValuePair<string, NoteData> kvp in Pages)
      {
        nd = kvp.Value;
        data.Add(nd);
      }

      XmlSerializer XS = new XmlSerializer(typeof(List<NoteData>));
      XS.Serialize(fs, data);
      fs.Dispose();
      */
    }
    //読込ボタン
    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
      tabDeleted = true;
      TegakiCanvas.Strokes.Clear();
      TegakiCanvas.Children.Clear();
      OCRCanvas.Strokes.Clear();
      OCRCanvas.Children.Clear();
      Pages.Clear();
      TabControl1.Items.Clear();
      tabCount = 0;
      AddAddTabButton();

      OpenFileDialog ofd = new OpenFileDialog() { AddExtension = true, DefaultExt = "note", Multiselect = false, CheckPathExists = true, CheckFileExists = true };
      ofd.ShowDialog();
      if (ofd.FileName == "" || !File.Exists(ofd.FileName)) return;
      FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
      XmlSerializer XS = new XmlSerializer(typeof(List<byte[]>));

      List<byte[]> DATA = (List<byte[]>)XS.Deserialize(fs);

      foreach (byte[] data in DATA)
      {
        tabCount++;
        XmlSerializer xs = new XmlSerializer(typeof(NoteData));
        MemoryStream ms = new MemoryStream(data);
        TabItem tab = new TabItem();
        Pages.Add("Page" + tabCount, (NoteData)xs.Deserialize(ms));
        AddTabHeaderWithCloseButton(tab, "Page" + tabCount);
        TabControl1.Items.Add(tab);
      }
      if (tabCount > 0)
      {
        TegakiCanvas.Strokes = new StrokeCollection(new MemoryStream(Pages["Page1"].tegaki));
        lineMargine = Pages["Page1"].borderMargin;
        for (int i = 0; i < Pages["Page1"].TBFontSizes.Count; i++)
        {
          TextBlock tb = new TextBlock() { Text = Pages["Page1"].TBtexts[i], FontSize = Pages["Page1"].TBFontSizes[i], LineHeight = Pages["Page1"].TBLineHeights[i] };
          TegakiCanvas.Children.Add(tb);
          InkCanvas.SetLeft(tb, Pages["Page1"].TBXs[i]);
          InkCanvas.SetTop(tb, Pages["Page1"].TBYs[i]);
        }
      }
      else
      {
        TabAddButton_Click(null, null);
      }
      fs.Dispose();
      lastSelectedTab = 1;
      TabControl1.SelectedIndex = 1;
      tabDeleted = false;
    }

    int tabCount = 0;
    //追加ボタン
    private void TabAddButton_Click(object sender, RoutedEventArgs e)
    {
      int tabPlus = 0;
      TabItem tab = new TabItem();
      tabCount += 1;
    retry:
      try
      {
        Pages.Add("Page" + (tabCount + tabPlus), new NoteData());
      }
      catch
      {
        tabPlus++;
        goto retry;
      }
      AddTabHeaderWithCloseButton(tab, "Page" + (tabCount + tabPlus));

      TabControl1.Items.Add(tab);
    }

    private void AddTabHeaderWithCloseButton(TabItem tabItem, string title)
    {
      DockPanel dock = new DockPanel();
      Button button = new Button()
      {
        Height = 16,
        FontSize = 8,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(2),
        Padding = new Thickness(2)
      };
      button.Content = "X";
      button.Click += (sender, e) =>
      {
        tabDeleted = true;
        tabCount--;
        if (TabControl1.SelectedIndex == tabCount) lastSelectedTab--;
        if (tabCount <= 1)
        {
          TabAddButton_Click(null, null);
          lastSelectedTab = 1;
        }
        if (lastSelectedTab >= tabCount) lastSelectedTab = tabCount - 1;
        if (lastSelectedTab == 0) lastSelectedTab = 1;
        TabControl1.Items.Remove(tabItem);
        Pages.Remove(title);
        tabDeleted = false;
      };

      TextBlock tb = new TextBlock()
      {
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
        Margin = new Thickness(2)
      };
      tb.Text = title;

      DockPanel.SetDock(button, Dock.Right);
      DockPanel.SetDock(tb, Dock.Left);

      dock.Children.Add(button);
      dock.Children.Add(tb);
      tabItem.Header = dock;
    }

    private void AddAddTabButton()
    {
      Button button = new Button()
      {
        Height = 16,
        FontSize = 8,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(2),
        Padding = new Thickness(2)
      };
      button.Content = "＋";
      button.Click += TabAddButton_Click;
      TabControl1.Items.Add(new TabItem() { Header = button });
    }


    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
      OCRCanvas_PreviewStylusUp(null, null);
    }

    private void root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      return;
      if (Mouse.LeftButton == MouseButtonState.Released)
      {
        gridStack.Children.Clear();
        DrawNoteLine(lineMargine);
      }
    }

    private void SettingButton_Click(object sender, RoutedEventArgs e)
    {
      SettingWindow sw = new SettingWindow();
      sw.ShowDialog();
      lineMargine = InkTestWpf.Properties.Settings.Default.Border;
      gridStack.Children.Clear();
      DrawNoteLine(lineMargine);
    }

    private void root_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      InkTestWpf.Properties.Settings.Default.Save();
    }

    private void TegakiCanvas_SelectionChanged(object sender, EventArgs e)
    {
      if (TegakiCanvas.GetSelectedElements().Count + TegakiCanvas.GetSelectedStrokes().Count != 0)
      {
        TegakiCanvas.ContextMenu = EditMenu;
      }
      else
      {
        TegakiCanvas.ContextMenu = NormalMenu;
      }
    }

    private void TegakiCanvas_PreviewStylusOutOfRange(object sender, StylusEventArgs e)
    {
      if (OCRButton.IsChecked == true)
      {
        Panel.SetZIndex(TegakiCanvas, 0);
      }
    }

    private void TabControl1_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (tabDeleted)
      {
        return;
      }
      //追加ボタン用タブを選択させない
      if (TabControl1.SelectedIndex <= 0)
      {
        TabControl1.SelectedIndex = lastSelectedTab;
        return;
      }
      else
      {
        MemoryStream ms = new MemoryStream();
        TegakiCanvas.Strokes.Save(ms, true);
        NoteData nd = new NoteData() { tegaki = ms.ToArray(), borderMargin = lineMargine };
        foreach (TextBlock TB in TegakiCanvas.Children.OfType<TextBlock>())
        {
          nd.TBFontSizes.Add(TB.FontSize);
          nd.TBLineHeights.Add(TB.LineHeight);
          nd.TBtexts.Add(TB.Text);
          nd.TBXs.Add(InkCanvas.GetLeft(TB));
          nd.TBYs.Add(InkCanvas.GetTop(TB));
        }
        Pages[(((TabControl1.Items[lastSelectedTab] as TabItem).Header as DockPanel).Children[1] as TextBlock).Text] = nd;

        lastSelectedTab = TabControl1.SelectedIndex;

        nd = Pages[(((TabControl1.Items[lastSelectedTab] as TabItem).Header as DockPanel).Children[1] as TextBlock).Text];

        TegakiCanvas.Strokes.Clear();
        TegakiCanvas.Children.Clear();
        OCRCanvas.Strokes.Clear();
        OCRCanvas.Children.Clear();

        if (nd.tegaki == null) return;

        TegakiCanvas.Strokes = new StrokeCollection(new MemoryStream(nd.tegaki));
        lineMargine = nd.borderMargin;
        for (int i = 0; i < nd.TBFontSizes.Count; i++)
        {
          TextBlock tb = new TextBlock() { Text = nd.TBtexts[i], FontSize = nd.TBFontSizes[i], LineHeight = nd.TBLineHeights[i] };
          TegakiCanvas.Children.Add(tb);
          InkCanvas.SetLeft(tb, nd.TBXs[i]);
          InkCanvas.SetTop(tb, nd.TBYs[i]);
        }
      }

    }

  }
}
