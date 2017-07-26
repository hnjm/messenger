using System.Windows;

namespace Messenger
{
    public partial class Fallback : Window
    {
        public Fallback()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static void Show(Window owner, string title, object content)
        {
            var msw = new Fallback();
            msw.Owner = owner;
            msw.textblockHeader.Text = title;
            msw.textboxContent.Text = content?.ToString();
            msw.WindowStartupLocation = (owner is null) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            msw.ShowDialog();
        }
    }
}
