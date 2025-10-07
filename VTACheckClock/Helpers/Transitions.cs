using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VTACheckClock.ViewModels;

namespace VTACheckClock.Helpers
{
    /// <summary>
    /// PageTransitions are used to render a transition between two views, for example in a Carousel or TransitioningContentControl
    /// </summary>
    public class PageTransition(string displayTitle) : ViewModelBase
    {
        public string DisplayTitle { get; } = displayTitle;

        private IPageTransition? _Transition;
        public IPageTransition? Transition
        {
            get { return _Transition; }
            set { this.RaiseAndSetIfChanged(ref _Transition, value); }
        }

        public override string ToString()
        {
            return DisplayTitle;
        }
    }

    /// <summary>
    /// You can also create your own PageTransition by implementing the IPageTransition-interface.
    /// </summary>
    public class CustomTransition : IPageTransition
    {
        public CustomTransition() { }

        public CustomTransition(TimeSpan duration)
        {
            Duration = duration;
        }

        public TimeSpan Duration { get; set; }

        public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var tasks = new List<Task>();
            //var parent = GetVisualParent(from, to);
            var scaleProperty = ScaleTransform.ScaleYProperty;

            if (from != null)
            {
                var animation = new Animation {
                    FillMode = FillMode.Forward,
                    Children = {
                        new KeyFrame { Setters = { new Setter { Property = scaleProperty, Value = 1d } }, Cue = new Cue(0d) },
                        new KeyFrame { Setters = { new Setter { Property = scaleProperty, Value = 0d } }, Cue = new Cue(1d) }
                    },
                    Duration = Duration
                };
                tasks.Add(animation.RunAsync(from, cancellationToken));
            }

            if (to != null)
            {
                to.IsVisible = true;
                var animation = new Animation {
                    FillMode = FillMode.Forward,
                    Children = {
                        new KeyFrame { Setters = { new Setter { Property = scaleProperty, Value = 0d } }, Cue = new Cue(0d) },
                        new KeyFrame { Setters = { new Setter { Property = scaleProperty, Value = 1d } }, Cue = new Cue(1d) }
                    },
                    Duration = Duration
                };
                tasks.Add(animation.RunAsync(to, cancellationToken));
            }

            await Task.WhenAll(tasks);

            if (from != null && !cancellationToken.IsCancellationRequested)
            {
                from.IsVisible = false;
            }
        }

        private static Visual GetVisualParent(Visual? from, Visual? to)
        {
            var p1 = (from ?? to)!.GetVisualParent();
            var p2 = (to ?? from)!.GetVisualParent();

            if (p1 != null && p2 != null && p1 != p2)
            {
                throw new ArgumentException("Controls for PageSlide must have same parent.");
            }

            return (Visual)(p1 ?? throw new InvalidOperationException("Cannot determine visual parent."));
        }
    }
}