using System.ComponentModel;
using System.Windows;

namespace PigPicPot.Views
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(string status, int percentage)
        {
            // 确保在UI线程上更新界面
            if (CheckAccess())
            {
                StatusText.Text = status;
                ProgressIndicator.Value = percentage;
                ProgressText.Text = $"{percentage}%";
            }
            else
            {
                Dispatcher.Invoke(() => {
                    StatusText.Text = status;
                    ProgressIndicator.Value = percentage;
                    ProgressText.Text = $"{percentage}%";
                });
            }
        }
        
        private void SplashScreenWindow_Closing(object sender, CancelEventArgs e)
        {
            // 当用户关闭启动画面时，关闭整个应用程序
            System.Windows.Application.Current.Shutdown();
        }
    }
}