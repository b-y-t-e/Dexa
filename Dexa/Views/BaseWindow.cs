using System.Windows;

namespace Dexa.Views
{
    public class BaseWindow : Window
    {
        public static readonly RoutedEvent WindowActivatedEvent = EventManager.RegisterRoutedEvent(
            "WindowActivated", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BaseWindow));

        public event RoutedEventHandler WindowActivated
        {
            add { AddHandler(WindowActivatedEvent, value); }
            remove { RemoveHandler(WindowActivatedEvent, value); }
        }

        public BaseWindow()
        {
            Activated += OnActivated;
        }

        private void OnActivated(object sender, System.EventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(WindowActivatedEvent));
        }
    }
}
