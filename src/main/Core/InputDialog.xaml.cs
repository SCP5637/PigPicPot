using System.Windows;

namespace PigPicPot.Core
{
    public partial class InputDialog : Window
    {
        public string InputText
        {
            get { return InputTextBox.Text; }
            set { InputTextBox.Text = value; }
        }

        public InputDialog(string prompt, string title, string defaultText = "")
        {
            InitializeComponent();
            
            this.Title = title;
            this.PromptText.Text = prompt;
            this.InputText = defaultText;
            
            Loaded += (s, e) => this.InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}