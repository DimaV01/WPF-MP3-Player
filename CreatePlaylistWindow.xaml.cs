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
using Microsoft.Win32;
using System.IO;
using NAudio.Wave;


namespace Blueberry
{

    public partial class CreatePlaylistWindow : Window
    {
        public Playlist NewPlaylist { get; private set; }

        public CreatePlaylistWindow()
        {
            InitializeComponent();
            NewPlaylist = new Playlist();
        }

        private void LoadCoverImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Image files (*.jpg;*.png)|*.jpg;*.png" };
            if (openFileDialog.ShowDialog() == true)
            {
                NewPlaylist.CoverImagePath = openFileDialog.FileName;
                coverImage.Source = new BitmapImage(new Uri(openFileDialog.FileName));
            }
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav", Multiselect = true };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    NewPlaylist.Tracks.Add(new Track
                    {
                        FileName = System.IO.Path.GetFileName(file),
                        FilePath = file,
                        Duration = GetDuration(file)
                    });
                }
            }
        }

        private TimeSpan GetDuration(string filePath)
        {
            using (var audioFile = new AudioFileReader(filePath))
            {
                return audioFile.TotalTime;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();  // Закрывает окно
        }


        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            NewPlaylist.Name = playlistName.Text;
            DialogResult = true;
        }
    }

}
