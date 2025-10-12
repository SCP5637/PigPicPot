namespace PigPicPot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);
            DebugConsole.Show();

            try
            {
                string configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.cfg");
                if (System.IO.File.Exists(configFile))
                {
                    var config = System.IO.File.ReadAllLines(configFile);
                    var langLine = config.FirstOrDefault(line => line.StartsWith("language="));
                    if (langLine != null)
                    {
                        var langCode = langLine.Split('=')[1].Trim();
                        var culture = new System.Globalization.CultureInfo(langCode);
                        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                        PigPicPot.Strings.Resources.Culture = culture;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting language: {ex.Message}");
            }
        }
    }

}
