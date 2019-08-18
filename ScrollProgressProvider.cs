using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace ShyHeaderPivot
{
    public class ScrollProgressProvider : DependencyObject
    {
        private readonly CompositionPropertySet propSet;
        private readonly ExpressionAnimation progressBind;
        private double lastOffset;
        private bool readyToScroll;
        private double innerProgress;

        public ScrollProgressProvider()
        {
            propSet = Window.Current.Compositor.CreatePropertySet();
            propSet.InsertScalar("progress", 0f);
            propSet.InsertScalar("threshold", 0f);

            progressBind = Window.Current.Compositor.CreateExpressionAnimation("clamp(prop.progress, 0f, 1f)");
            progressBind.SetReferenceParameter("prop", propSet);
        }


        public ScrollViewer ScrollViewer
        {
            get { return (ScrollViewer)GetValue(ScrollViewerProperty); }
            set { SetValue(ScrollViewerProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ScrollViewer.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ScrollViewerProperty =
            DependencyProperty.Register("ScrollViewer", typeof(ScrollViewer), typeof(ScrollProgressProvider), new PropertyMetadata(null, (s, a) =>
            {
                if (a.NewValue != a.OldValue)
                {
                    if (s is ScrollProgressProvider sender)
                    {
                        sender.ScrollViewerChanged(a.OldValue as ScrollViewer, a.NewValue as ScrollViewer);
                    }
                }
            }));

        public double Threshold
        {
            get { return (double)GetValue(ThresholdProperty); }
            set { SetValue(ThresholdProperty, value); }
        }

        public static readonly DependencyProperty ThresholdProperty =
            DependencyProperty.Register("Threshold", typeof(double), typeof(ScrollProgressProvider), new PropertyMetadata(0d, (s, a) =>
            {
                if (a.NewValue != a.OldValue)
                {
                    if (s is ScrollProgressProvider sender)
                    {
                        var val = (double)a.NewValue;
                        if (val < 0)
                            throw new ArgumentException($"{nameof(Threshold)}不能小于0");

                        sender.propSet.InsertScalar("threshold", (float)val);
                        sender.OnProgressChanged();
                    }
                }
            }));



        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(ScrollProgressProvider), new PropertyMetadata(0d, (s, a) =>
            {
                if (a.NewValue != a.OldValue)
                {
                    if ((double)a.NewValue > 1)
                        throw new ArgumentException($"{nameof(Progress)}不能大于1");

                    if ((double)a.NewValue < 0)
                        throw new ArgumentException($"{nameof(Progress)}不能小于0");

                    if (s is ScrollProgressProvider sender)
                    {
                        if (sender.innerProgress != (double)a.NewValue)
                        {
                            sender.SyncScrollView(sender.ScrollViewer);
                        }
                        sender.OnProgressChanged();
                    }
                }
            }));

        private void ScrollViewerChanged(ScrollViewer oldSv, ScrollViewer newSv)
        {
            if (oldSv != null)
            {
                oldSv.ViewChanged -= ScrollViewer_ViewChanged;
                oldSv.Unloaded -= ScrollViewer_Unloaded;

                propSet.InsertScalar("progress", (float)innerProgress);
            }

            if (newSv != null)
            {
                readyToScroll = true;

                if (newSv.VerticalOffset < Threshold || lastOffset < Threshold || (newSv.VerticalOffset > Threshold && lastOffset > Threshold))
                    SyncScrollView(newSv);

                newSv.ViewChanged += ScrollViewer_ViewChanged;
                newSv.Unloaded += ScrollViewer_Unloaded;
            }
        }

        private void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            ((ScrollViewer)sender).Unloaded -= ScrollViewer_Unloaded;
            ((ScrollViewer)sender).ViewChanged -= ScrollViewer_ViewChanged;
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            lastOffset = ((ScrollViewer)sender).VerticalOffset;
            innerProgress = GetProgress(lastOffset, Threshold);
            Progress = innerProgress;

            if (readyToScroll)
            {
                readyToScroll = false;
                StartScrollBind((ScrollViewer)sender);
            }
        }

        CancellationTokenSource cts;

        private async void StartScrollBind(ScrollViewer sv)
        {
            if (cts != null)
                cts.Cancel();

            cts = new CancellationTokenSource();

            innerProgress = GetProgress(lastOffset, Threshold);
            try
            {
                if (sv.VerticalOffset > 0)
                    await Task.Delay(150, cts.Token);

                propSet.InsertScalar("progress", (float)innerProgress);

                var svp = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(sv);

                var exp = Window.Current.Compositor.CreateExpressionAnimation($"clamp(-sv.Translation.Y, 0f, prop.threshold) / prop.threshold");
                exp.SetReferenceParameter("sv", svp);
                exp.SetReferenceParameter("prop", propSet);

                propSet.StartAnimation("progress", exp);
            }
            catch
            {

            }
            Progress = innerProgress;

        }

        private void SyncScrollView(ScrollViewer sv)
        {
            if (sv == null) return;

            sv.ChangeView(null, Threshold * Progress, null, true);
        }

        public CompositionPropertySet CreatePropertySet()
        {
            var _propSet = Window.Current.Compositor.CreatePropertySet();
            _propSet.InsertScalar("progress", 0f);
            _propSet.StartAnimation("progress", progressBind);
            return _propSet;
        }

        public event TypedEventHandler<object, double> ProgressChanged;
        protected void OnProgressChanged()
        {
            ProgressChanged?.Invoke(this, Progress);

            System.Diagnostics.Debug.WriteLine(Progress);
        }


        private static double GetProgress(double offset, double threshold)
        {
            if (threshold == 0) return 0;
            return Math.Min(1, Math.Max(0, offset / threshold));
        }

    }
}
