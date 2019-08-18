using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace ShyHeaderPivot
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            var rnd = new Random();
            var colors = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(c => (Color)c.GetValue(null)).ToArray();
            list = Enumerable.Range(1, 19).Select(c => new Model()
            {
                Title = $"第{c}个标题",
                Brushes = Enumerable.Range(0, 50).Select(x => colors[rnd.Next(colors.Length)]).Select(x => new SolidColorBrush(x)).ToList()
            }).ToList();
        }

        const float endOffsetValue = -150;

        public List<Model> list { get; }
        ScrollViewer sv;
        float offset;
        CancellationTokenSource cts;
        bool readyToStart;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var gv = ElementCompositionPreview.GetElementVisual(Target);
            var tv = ElementCompositionPreview.GetElementVisual(HeaderText);

            var progress = $"clamp(host.Offset.Y / {endOffsetValue}f, 0f ,1f)";

            var startOffset = "Vector3((host.Size.X - this.Target.Size.X) / 2, (host.Size.Y - 50 - this.Target.Size.Y) / 2, 1f)";
            var endOffset = $"Vector3(0f, -{endOffsetValue}f, 1f)";

            var scale = "(50f / this.Target.Size.Y)";
            var startScale = "Vector3(1f, 1f, 1f)";
            var endScale = $"Vector3({scale}, {scale}, 1f)";

            var offsetExp = Window.Current.Compositor.CreateExpressionAnimation($"lerp({startOffset}, {endOffset}, {progress})");
            var scaleExp = Window.Current.Compositor.CreateExpressionAnimation($"lerp({startScale}, {endScale}, {progress})");

            offsetExp.SetReferenceParameter("host", gv);
            scaleExp.SetReferenceParameter("host", gv);

            tv.StartAnimation("Offset", offsetExp);
            tv.StartAnimation("Scale", scaleExp);
        }

        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var container = Pivot.ContainerFromItem(Pivot.SelectedItem) as PivotItem;

            var gv = ElementCompositionPreview.GetElementVisual(Target);
            gv.Offset = new System.Numerics.Vector3(0, offset, 0);

            if (cts != null) cts.Cancel();
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var contentTemplateRoot = await WaitForLoaded(container, () => container.ContentTemplateRoot as FrameworkElement, c => c != null, cts.Token);

            if (sv != null)
                sv.ViewChanged -= Sv_ViewChanged;

            sv = contentTemplateRoot.FindName("sv") as ScrollViewer;

            sv = ((FrameworkElement)container.ContentTemplateRoot).FindName("sv") as ScrollViewer;
            InitScrollViewer(sv);
        }

        private void Sv_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            offset = Math.Max(endOffsetValue, Math.Min(0, (float)-sv.VerticalOffset));
            if (e.IsIntermediate && readyToStart)
            {
                readyToStart = false;
                StartScrollBind((ScrollViewer)sender);
            }
        }

        private void StartScrollBind(ScrollViewer sv)
        {
            var sp = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(sv);

            var exp = Window.Current.Compositor.CreateExpressionAnimation($"clamp(prop.Translation.Y, {endOffsetValue}f, 0f)");
            exp.SetReferenceParameter("prop", sp);

            var gv = ElementCompositionPreview.GetElementVisual(Target);
            gv.StartAnimation("Offset.Y", exp);
        }

        private void InitScrollViewer(ScrollViewer sv)
        {
            if (sv == null) return;
            readyToStart = true;

            if (sv.VerticalOffset < 200 || offset < endOffsetValue)
                sv.ChangeView(null, -offset, null, true);

            sv.ViewChanged += Sv_ViewChanged;
        }

        private async Task<T> WaitForLoaded<T>(FrameworkElement element, Func<T> func, Predicate<T> pre, CancellationToken cancellationToken)
        {
            TaskCompletionSource<T> tcs = null;
            try
            {
                tcs = new TaskCompletionSource<T>();
                cancellationToken.ThrowIfCancellationRequested();
                var result = func.Invoke();
                if (pre(result)) return result;


                element.Loaded += Element_Loaded;

                return await tcs.Task;

            }
            catch
            {
                element.Loaded -= Element_Loaded;
                var result = func.Invoke();
                if (pre(result)) return result;
            }

            return default;


            void Element_Loaded(object sender, RoutedEventArgs e)
            {
                if (tcs == null) return;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    element.Loaded -= Element_Loaded;
                    var _result = func.Invoke();
                    if (pre(_result)) tcs.SetResult(_result);
                    else tcs.SetCanceled();
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("canceled");
                }
            }

        }

    }

    public class Model
    {
        public string Title { get; set; }

        public List<SolidColorBrush> Brushes { get; set; }
    }
}
