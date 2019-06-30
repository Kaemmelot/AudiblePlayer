using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RfidProgrammer.MainView
{
    public partial class ConnectionStatusBar : UserControl, INotifyPropertyChanged
    {
        public ConnectionStatusBar()
        {
            InitializeComponent();
        }

        public Action ComClickAction
        {
            get { return (Action)GetValue(ComClickActionProperty); }
            set
            {
                SetValue(ComClickActionProperty, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComClickAction)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComCursor)));
            }
        }

        public static readonly DependencyProperty ComClickActionProperty =
            DependencyProperty.Register("ComClickAction", typeof(Action), typeof(ConnectionStatusBar), new PropertyMetadata(null, new PropertyChangedCallback(ComClickActionChanged)));

        private static void ComClickActionChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            // this is needed since the setter of ComClickAction is never called by the property
            var propChanged = (obj as ConnectionStatusBar).PropertyChanged;
            if (propChanged != null)
            {
                propChanged.Invoke(obj, new PropertyChangedEventArgs(nameof(ComClickAction)));
                propChanged.Invoke(obj, new PropertyChangedEventArgs(nameof(ComCursor)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Cursor ComCursor => ComClickAction != null ? Cursors.Hand : null;

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ComClickAction?.Invoke();
        }
    }
}
