using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace In_game_compass
{
    public partial class MainWindow : Window
    {
        // 常量定义
        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExLayered = 0x00080000;

        // 鼠标钩子相关
        private const int WhMouseLl = 14;
        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHookId = IntPtr.Zero;

        // 鼠标移动和角度计算
        private int _lastMouseX;
        private double _accumulatedAngle = 0;
        private double _sensitivityCoefficient = 0.10; // 默认鼠标灵敏度系数
        
        // 指南针刻度
        private const int TotalDegrees = 90;
        private const double PixelsPerDegree = 8; // 每度对应的像素数，可根据需要调整
        private const int MarkingInterval = 5; // 每隔多少度绘制一个刻度
        
        private const double OffsetCorrection = 1.88 * PixelsPerDegree;  // 每度对应的像素值 * 2 度的修正


        
        
        public MainWindow()
        {
            InitializeComponent(); 
            // InitializeCompassMarkings();
            Loaded += MainWindow_Loaded;
            
            // 创建指南针图案
            Canvas compassPattern = CreateCompassPattern();

            // 将其设置为 VisualBrush
            CompassBrush.Visual = compassPattern;
            CompassBrush.TileMode = TileMode.Tile;
            CompassBrush.ViewportUnits = BrushMappingMode.Absolute;
            CompassBrush.Stretch = Stretch.None;
            CompassBrush.Viewport = new Rect(0, 0, compassPattern.Width, compassPattern.Height);
            
            // 设置全局鼠标钩子
            _mouseProc = MouseHookCallback;
            _mouseHookId = SetMouseHook(_mouseProc);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr extendedStyle = GetWindowLongPtr(hwnd, GwlExstyle);

            // 合并新的扩展样式
            var newStyle = new IntPtr(extendedStyle.ToInt64() | WsExLayered | WsExTransparent);
            IntPtr previousStyle = SetWindowLongPtr(hwnd, GwlExstyle, newStyle);

            if (previousStyle != IntPtr.Zero) return;
            int error = Marshal.GetLastPInvokeError();
            if (error != 0)
            {
                // 处理错误
                MessageBox.Show($"SetWindowLongPtr 失败，错误代码：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 设置鼠标钩子
        // 设置鼠标钩子
        private unsafe IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                delegate* unmanaged<int, IntPtr, IntPtr, IntPtr> functionPointer = (delegate* unmanaged<int, IntPtr, IntPtr, IntPtr>)Marshal.GetFunctionPointerForDelegate(proc);

                return SetWindowsHookEx(WhMouseLl, functionPointer, moduleHandle, 0);
            }
        }


        // 鼠标钩子回调函数
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            int WM_MOUSEMOVE = 0x0200;
            if (wParam != (IntPtr)WM_MOUSEMOVE)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            Msllhookstruct hookStruct = Marshal.PtrToStructure<Msllhookstruct>(lParam);
            int deltaX = hookStruct.pt.x - _lastMouseX;
            _lastMouseX = hookStruct.pt.x;

            // 计算角度变化
            double deltaAngle = deltaX * _sensitivityCoefficient;
            _accumulatedAngle += deltaAngle;

            // 更新指南针
            UpdateCompass(_accumulatedAngle);
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
        
         private Canvas CreateCompassPattern()
        {
            Canvas patternCanvas = new Canvas();
            double patternWidth = 360 * PixelsPerDegree; // 360度对应的宽度
            patternCanvas.Width = patternWidth;
            patternCanvas.Height = 50;

            for (int degree = 0; degree <= 360; degree += MarkingInterval)
            {
                double x = degree * PixelsPerDegree;

                // 绘制刻度线
                Line line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = (degree % 90 == 0) ? 20 : 10,
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };
                patternCanvas.Children.Add(line);

                // 添加度数文本
                if (degree % MarkingInterval == 0)
                {
                    TextBlock degreeText = new TextBlock
                    {
                        Text = degree.ToString(),
                        Foreground = Brushes.White,
                        FontSize = 10
                    };
                    Canvas.SetLeft(degreeText, x - 10);
                    Canvas.SetTop(degreeText, 20);
                    patternCanvas.Children.Add(degreeText);
                }

                // 添加主要方向（N、E、S、W）
                if (degree % 90 == 0)
                {
                    string direction = degree switch
                    {
                        0 => "N",
                        90 => "E",
                        180 => "S",
                        270 => "W",
                        360 => "N", // 确保360度显示"N"
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(direction))
                    {
                        TextBlock directionText = new TextBlock
                        {
                            Text = direction,
                            Foreground = Brushes.White,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold
                        };
                        Canvas.SetLeft(directionText, x - 10);
                        Canvas.SetTop(directionText, -15);
                        patternCanvas.Children.Add(directionText);
                    }
                }
            }

            return patternCanvas;
        }
         
        private void UpdateCompass(double angle)
        {
            Dispatcher.Invoke(() =>
            {
                // 确保角度在 0 到 360 度之间
                angle = (angle % 360 + 360) % 360;
                

                // 更新当前方位的显示
                HeadingText.Text = $"{(int)Math.Round(angle)}°";

                // 计算滚动的偏移量，使得箭头下方的刻度与角度对齐
                double offset = angle * PixelsPerDegree;

                // 获取 Grid 宽度的一半，确保箭头指示的刻度在中心位置
                double gridCenterOffset = ActualWidth / 2;

                // 应用平移变换，确保红色箭头正下方显示正确的角度
                CompassBrush.Transform = new TranslateTransform(-offset + gridCenterOffset - OffsetCorrection, 0);
            });
        }
        
//         private void InitializeCompassMarkings()
// {
//     CompassCanvas.Children.Clear();
//
//     // 定义可见范围内的角度跨度
//     int visibleAngleRange = 180; // 可视范围为 ±90 度
//
//     // 计算可见范围内的刻度数量
//     int totalMarks = (visibleAngleRange * 2) / MarkingInterval;
//
//     for (int i = -visibleAngleRange; i <= visibleAngleRange; i += MarkingInterval)
//     {
//         double degree = i;
//         double normalizedDegree = (degree + _accumulatedAngle) % 360;
//         if (normalizedDegree < 0)
//             normalizedDegree += 360;
//
//         double x = degree * PixelsPerDegree + (CompassCanvas.ActualWidth / 2);
//
//         // 绘制刻度线
//         var line = new Line
//         {
//             X1 = x,
//             Y1 = 0,
//             X2 = x,
//             Y2 = (normalizedDegree % 90 == 0) ? 20 : 10,
//             Stroke = Brushes.White,
//             StrokeThickness = 1
//         };
//         CompassCanvas.Children.Add(line);
//
//         // 添加度数文本
//         var degreeText = new TextBlock
//         {
//             Text = ((int)normalizedDegree).ToString(),
//             Foreground = Brushes.White,
//             FontSize = 10
//         };
//         
//         Canvas.SetLeft(degreeText, x);
//         Canvas.SetTop(degreeText, 20);
//         CompassCanvas.Children.Add(degreeText);
//
//         // 添加主要方向（N、E、S、W）
//         if (normalizedDegree % 90 != 0) continue;
//         string direction = normalizedDegree switch
//         {
//             0 => "N",
//             90 => "E",
//             180 => "S",
//             270 => "W",
//             _ => ""
//         };
//
//         if (string.IsNullOrEmpty(direction)) continue;
//         TextBlock directionText = new TextBlock
//         {
//             Text = direction,
//             Foreground = Brushes.White,
//             FontSize = 12,
//             FontWeight = FontWeights.Bold
//         };
//         const int offset = 3;
//         Canvas.SetLeft(directionText, x - offset);
//         Canvas.SetTop(directionText, -15);
//         CompassCanvas.Children.Add(directionText);
//     }
// }




        // 应用灵敏度按钮的点击事件
        private void ApplySensitivityButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(SensitivityTextBox.Text, out double sensitivity))
            {
                _sensitivityCoefficient = sensitivity;
            }
            else
            {
                MessageBox.Show("请输入有效的数字。");
            }
        }

        // 校准按钮的点击事件
        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            _accumulatedAngle = 0;
            UpdateCompass(_accumulatedAngle);
        }

        // 释放钩子
        protected override void OnClosed(EventArgs e)
        {
            UnhookWindowsHookEx(_mouseHookId);
            base.OnClosed(e);
        }

        // P/Invoke 声明
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong);


        [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
        private static unsafe partial IntPtr SetWindowsHookEx(int idHook, delegate* unmanaged<int, IntPtr, IntPtr, IntPtr> lpfn, IntPtr hMod, uint dwThreadId);


        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnhookWindowsHookEx(IntPtr hhk);

        [LibraryImport("user32.dll", SetLastError = true)]
        private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr GetModuleHandle(string lpModuleName);


        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private unsafe delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);



        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Msllhookstruct
        {
            public Point pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }
    }
}
