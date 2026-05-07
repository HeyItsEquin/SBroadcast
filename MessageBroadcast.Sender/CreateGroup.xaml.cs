using MessageBroadcast.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MessageBroadcast.Sender
{
    public partial class CreateGroup : Window
    {
        public GroupInfo? group;

        public CreateGroup()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupNameInput.Text))
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }

            var groupName = GroupNameInput.Text;
            if (groupName.Length > 20)
            {
                MessageBox.Show("Group name cannot be longer than 20 characters");
                e.Handled = true;
                return;
            }

            group = new GroupInfo
            {
                GroupName = GroupNameInput.Text,
                GroupMembers = []
            };

            e.Handled = true;
            DialogResult = true;
            Close();
        }
    }
}
