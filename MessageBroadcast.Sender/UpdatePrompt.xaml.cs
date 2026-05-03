using System.Windows;

namespace MessageBroadcast.Sender
{
    public enum UpdatePromptResult
    {
        SkipVersion,
        Decline,
        Accept
    }

    public partial class UpdatePrompt : Window
    {
        public UpdatePromptResult Result = UpdatePromptResult.Decline;

        public UpdatePrompt()
        {
            InitializeComponent();
        }

        private void AcceptUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.Accept;
            Close();
        }

        private void CancelUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.Decline;
            Close();
        }

        private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdatePromptResult.SkipVersion;
            Close();
        }
    }
}
