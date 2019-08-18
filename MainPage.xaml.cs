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

            provider = new ScrollProgressProvider();
            provider.Threshold = 150d;
        }

        const float endOffsetValue = -150;

        public List<Model> list { get; }
        float offset;
        CancellationTokenSource cts;
        ScrollProgressProvider provider;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var gv = ElementCompositionPreview.GetElementVisual(Target);
            var tv = ElementCompositionPreview.GetElementVisual(HeaderText);

            var startOffset = "Vector3((host.Size.X - this.Target.Size.X) / 2, (host.Size.Y - 50 - this.Target.Size.Y) / 2, 1f)";
            var endOffset = $"Vector3(0f, -{endOffsetValue}f, 1f)";

            var scale = "(50f / this.Target.Size.Y)";
            var startScale = "Vector3(1f, 1f, 1f)";
            var endScale = $"Vector3({scale}, {scale}, 1f)";

            var offsetExp = Window.Current.Compositor.CreateExpressionAnimation($"lerp({startOffset}, {endOffset}, provider.progress)");
            var scaleExp = Window.Current.Compositor.CreateExpressionAnimation($"lerp({startScale}, {endScale}, provider.progress)");

            var providerProp = provider.CreatePropertySet();

            offsetExp.SetReferenceParameter("host", gv);
            offsetExp.SetReferenceParameter("provider", providerProp);
            scaleExp.SetReferenceParameter("host", gv);
            scaleExp.SetReferenceParameter("provider", providerProp);

            tv.StartAnimation("Offset", offsetExp);
            tv.StartAnimation("Scale", scaleExp);

            var gvOffsetExp = Window.Current.Compositor.CreateExpressionAnimation("Vector3(0f, -150f * provider.progress, 0f)");
            gvOffsetExp.SetReferenceParameter("provider", providerProp);

            gv.StartAnimation("Offset", gvOffsetExp);
        }

        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var container = Pivot.ContainerFromItem(Pivot.SelectedItem) as PivotItem;

            if (cts != null) cts.Cancel();
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var contentTemplateRoot = await WaitForLoaded(container, () => container.ContentTemplateRoot as FrameworkElement, c => c != null, cts.Token);

            provider.ScrollViewer = contentTemplateRoot.FindName("sv") as ScrollViewer;
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
