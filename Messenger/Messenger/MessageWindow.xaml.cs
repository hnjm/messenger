using System.Windows;

namespace Messenger
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        public MessageWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static void Show(Window owner, string title, object content)
        {
            var msw = new MessageWindow();
            msw.Owner = owner;
            msw.textblockHeader.Text = title;
            msw.textboxContent.Text = content?.ToString();
            msw.WindowStartupLocation = (owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner);
            msw.ShowDialog();
        }
    }
}
