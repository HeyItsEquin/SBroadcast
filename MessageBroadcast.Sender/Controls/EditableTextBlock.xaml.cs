using Makaretu.Dns;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MessageBroadcast.Sender.Controls
{
    public partial class EditableTextBlock : UserControl
    {
        private bool _isEditing = false;
        private string _originalText = "";

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(EditableTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public event EventHandler<string>? TextCommitted;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public EditableTextBlock()
        {
            InitializeComponent();
        }

        public void BeginEdit()
        {
            _isEditing = true;
            _originalText = Text;

            TextBlockDisplay.Visibility = Visibility.Collapsed;
            TextBoxEdit.Visibility = Visibility.Visible;
            TextBoxEdit.Focus();
            TextBoxEdit.SelectAll();
        }

        private void TextBoxEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isEditing) return;

            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
        }

        private void TextBoxEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isEditing)
                CancelEdit();
        }

        private void CommitEdit()
        {
            var newText = string.IsNullOrEmpty(TextBoxEdit.Text)
                ? null
                : TextBoxEdit.Text.Trim();

            Text = newText!;
            TextCommitted?.Invoke(this, newText!);

            ExitEditMode();
        }

        private void CancelEdit()
        {
            Text = _originalText;
            ExitEditMode();
        }

        private void ExitEditMode()
        {
            _isEditing = false;
            TextBoxEdit.Visibility = Visibility.Collapsed;
            TextBlockDisplay.Visibility = Visibility.Visible;
        }
    }
}
