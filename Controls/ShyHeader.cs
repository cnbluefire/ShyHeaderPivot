using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

// The Templated Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234235

namespace ShyHeaderPivot.Controls
{
    public sealed class ShyHeader : ListBox
    {
        #region Const Values

        private static readonly Vector2 c_frame1point1 = new Vector2(0.9f, 0.1f);
        private static readonly Vector2 c_frame1point2 = new Vector2(0.7f, 0.4f);
        private static readonly Vector2 c_frame2point1 = new Vector2(0.1f, 0.9f);
        private static readonly Vector2 c_frame2point2 = new Vector2(0.2f, 1f);

        #endregion Const Values

        public ShyHeader()
        {
            this.DefaultStyleKey = typeof(ShyHeader);

            this.SelectionChanged += ShyHeader_SelectionChanged;
        }

        private void ShyHeader_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.SelectionMode != SelectionMode.Single) return;

            var oldIndicator = GetIndicator(e.RemovedItems.FirstOrDefault());
            if (oldIndicator == null) return;
            var newIndicator = GetIndicator(e.AddedItems.FirstOrDefault());
            if (newIndicator == null) return;

            TryStartAnimationWithScale(newIndicator, oldIndicator);
        }

        private Rectangle GetIndicator(object item)
        {
            if (item == null) return null;
            var container = ContainerFromItem(item);
            if (container == null) return null;

            var grid = VisualTreeHelper.GetChild(container, 0) as Grid;
            if (grid == null) return null;
            return grid.FindName("Indicator") as Rectangle;
        }

        private void TryStartAnimationWithScale(FrameworkElement newIndicator, FrameworkElement oldIndicator)
        {
            var compositor = Window.Current.Compositor;

            var old_target = ElementCompositionPreview.GetElementVisual(oldIndicator);
            var new_target = ElementCompositionPreview.GetElementVisual(newIndicator);

            old_target.Offset = Vector3.Zero ;
            old_target.CenterPoint = Vector3.Zero;
            old_target.Scale = Vector3.One;

            var oldSize = new Vector2((float)oldIndicator.ActualWidth, (float)oldIndicator.ActualHeight);
            var newSize = new Vector2((float)newIndicator.ActualWidth, (float)newIndicator.ActualHeight);

            var oldScale = oldSize / newSize;
            if (oldScale.Y < 0) return;

            var oldOffset = newIndicator.TransformToVisual(oldIndicator).TransformPoint(new Windows.Foundation.Point(0, 0)).ToVector2();

            float startx = 0, endx = 0, starty = 0, endy = 0;

            if (oldOffset.X > 0)
            {
                endx = newSize.X;
            }
            else
            {
                startx = newSize.X;
                oldOffset.X = oldOffset.X + newSize.X - oldSize.X;
            }

            if (oldOffset.Y > 0)
            {
                endy = newSize.Y;
            }
            else
            {
                starty = newSize.Y;
                oldOffset.Y = oldOffset.Y + newSize.Y - oldSize.Y;
            }

            var duration = TimeSpan.FromSeconds(0.6d);

            var standard = compositor.CreateCubicBezierEasingFunction(new Vector2(0.8f, 0.0f), new Vector2(0.2f, 1.0f));

            var singleStep = compositor.CreateStepEasingFunction();
            singleStep.IsFinalStepSingleFrame = true;

            var centerAnimation = compositor.CreateVector3KeyFrameAnimation();
            centerAnimation.InsertExpressionKeyFrame(0f, "Vector3(startx,starty,0f)", singleStep);
            centerAnimation.InsertExpressionKeyFrame(0.333f, "Vector3(endx,endy,0f)", singleStep);
            centerAnimation.SetScalarParameter("startx", startx);
            centerAnimation.SetScalarParameter("starty", starty);
            centerAnimation.SetScalarParameter("endx", endx);
            centerAnimation.SetScalarParameter("endy", endy);
            centerAnimation.Duration = duration;

            var offsetAnimation = compositor.CreateVector2KeyFrameAnimation();
            offsetAnimation.InsertExpressionKeyFrame(0f, "-oldOffset", singleStep);
            offsetAnimation.InsertExpressionKeyFrame(0.333f, "This.StartingValue", singleStep);
            offsetAnimation.SetVector2Parameter("oldOffset", oldOffset);
            offsetAnimation.Duration = duration;

            var scaleAnimation = compositor.CreateVector2KeyFrameAnimation();
            scaleAnimation.InsertExpressionKeyFrame(0f, "oldScale", standard);
            scaleAnimation.InsertExpressionKeyFrame(0.333f, "(target.Size + abs(oldOffset)) / target.Size",
                compositor.CreateCubicBezierEasingFunction(c_frame1point1, c_frame1point2));
            scaleAnimation.InsertExpressionKeyFrame(1f, "this.StartingValue",
                compositor.CreateCubicBezierEasingFunction(c_frame2point1, c_frame2point2));
            scaleAnimation.SetVector2Parameter("oldScale", oldScale);
            scaleAnimation.SetVector2Parameter("oldOffset", oldOffset);
            scaleAnimation.SetReferenceParameter("target", new_target);
            scaleAnimation.SetReferenceParameter("old", old_target);
            scaleAnimation.Duration = duration;

            new_target.StartAnimation("CenterPoint", centerAnimation);
            new_target.StartAnimation("Offset.XY", offsetAnimation);
            new_target.StartAnimation("Scale.XY", scaleAnimation);
        }
    }
}
