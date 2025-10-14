using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PigPicPot.Core
{
    internal static class SpecialFeatures
    {
        private struct SecretFeature
        {
            public string Name;
            public string FileName;
            public int Width;
            public int Height;
            public string Hash;
            public string ButtonName;
            public RoutedEventHandler ClickHandler;
        }

        private static readonly SecretFeature[] Features = new[]
        {
            new SecretFeature
            {
                Name = "MainWindowSecret",
                FileName = "zhu3.jpg",
                Width = 1920,
                Height = 1176,
                Hash = "0628b9d8d23d7a695938425fef17f9da4643246f4e410ab44461bdae8349a303",
                ButtonName = "InfoButton",
                ClickHandler = (sender, args) => 
                {
                    var infoWindow = new InfoWindow
                    {
                        Owner = System.Windows.Application.Current.MainWindow
                    };
                    infoWindow.ShowDialog();
                }
            },
            new SecretFeature
            {
                Name = "MiniModeSecret",
                FileName = "zhu1.png",
                Width = 640,
                Height = 480,
                Hash = "a4e0018caa82f60fa9d0eed8b472430ca4b48d8fc07f5d8bac6c7b8fd4263833",
                ButtonName = "MiniInfoButton",
                ClickHandler = (sender, args) =>
                {
                    var ownerWindow = System.Windows.Window.GetWindow(sender as System.Windows.DependencyObject);
                    var miniInfoWindow = new MiniInfoWindow
                    {
                        Owner = ownerWindow
                    };
                    miniInfoWindow.ShowDialog();
                }
            }
        };

        private static string ComputeFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static void CheckAndEnableFeatures(Window window, string imagePath, int imageWidth, int imageHeight)
        {
            if (!File.Exists(imagePath)) return;

            string currentHash = ComputeFileHash(imagePath);
            string currentFileName = Path.GetFileName(imagePath);

            foreach (var feature in Features)
            {
                if (currentFileName.Equals(feature.FileName, StringComparison.OrdinalIgnoreCase) &&
                    imageWidth == feature.Width && imageHeight == feature.Height &&
                    currentHash.Equals(feature.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    var button = window.FindName(feature.ButtonName) as System.Windows.Controls.Button;
                    if (button != null)
                    {
                        // First, remove any existing handlers to prevent duplicates
                        button.Click -= feature.ClickHandler;
                        // Then, add the new handler
                        button.Click += feature.ClickHandler;
                        
                        button.Visibility = Visibility.Visible;
                    }
                }
            }
        }
    }
}
