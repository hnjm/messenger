using System.Windows;

namespace Messenger
{
    public partial class Fallback : Window
    {
        public Fallback()
        {
            InitializeComponent();
        }

        private void _Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static void Show(Window owner, string title, object content)
        {
            var msw = new Fallback();
            msw.Owner = owner;
            msw.uiHeadText.Text = title;
            msw.uiContentText.Text = content?.ToString();
            msw.WindowStartupLocation = (owner is null) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            msw.ShowDialog();
        }
    }
}
