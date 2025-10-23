using System.Windows;

namespace PigPicPot.Core
{
    public partial class ConfirmDialog : Window
    {
        public bool Result { get; private set; } = false;

        public ConfirmDialog(string message, string title)
        {
            InitializeComponent();
            
            this.Title = title;
            this.MessageText.Text = message;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Result = true;
            this.DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            this.Result = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}